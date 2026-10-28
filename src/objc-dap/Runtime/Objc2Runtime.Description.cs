using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Globalization;
using System.Text;

namespace Objc.Dap.Runtime
{
    // Mirrors the output of GNUstep's -[NSObject description] without running code in the target.
    public partial class Objc2Runtime
    {
        // Bounds the total number of collection elements formatted, which also guards against cycles.
        private const int MaxDescriptionElements = 1000;

        /// <summary>
        /// Gets the text GNUstep's <c>-description</c> would return for <paramref name="object"/>, or null if the object can't be read.
        /// </summary>
        public string? GetObjectDescription(Objc2Object @object)
        {
            ArgumentNullException.ThrowIfNull(@object);

            int budget = MaxDescriptionElements;
            return this.Describe(@object, ref budget);
        }

        private string? Describe(Objc2Object @object, ref int budget)
        {
            if (@object == Objc2Object.Null)
            {
                return null;
            }

            if (@object.IsSmallObject)
            {
                return Enum.IsDefined(@object.SmallObjectType) ? FormatValue(this.GetObjectValue(@object)) : null;
            }

            var className = this.GetClassName(@object);

            if (className == null)
            {
                return null;
            }

            try
            {
                if (this.ArrayClassNames.Contains(className))
                {
                    return this.DescribeArray(@object, className, ref budget);
                }

                switch (className)
                {
                    case "NSFloatNumber":
                        return FormatG(this.ReadFloat(@object.Address + 8), 7);

                    case "NSURL":
                        return this.DescribeUrl(@object, ref budget);

                    case "GSPointValue":
                        return $"{{x = {FormatG(this.ReadDouble(@object.Address + 8), 6)}; y = {FormatG(this.ReadDouble(@object.Address + 16), 6)}}}";

                    case "GSSizeValue":
                        return $"{{width = {FormatG(this.ReadDouble(@object.Address + 8), 6)}; height = {FormatG(this.ReadDouble(@object.Address + 16), 6)}}}";

                    case "GSRectValue":
                        return $"{{x = {FormatG(this.ReadDouble(@object.Address + 8), 6)}; y = {FormatG(this.ReadDouble(@object.Address + 16), 6)}; "
                            + $"width = {FormatG(this.ReadDouble(@object.Address + 24), 6)}; height = {FormatG(this.ReadDouble(@object.Address + 32), 6)}}}";
                }

                if (this.readers.TryGetValue(className, out var reader))
                {
                    var description = FormatValue(reader(@object));

                    if (description != null)
                    {
                        return description;
                    }
                }
            }
            catch (Exception ex) when (ex is Win32Exception or IOException or ArgumentException or NotSupportedException or OverflowException)
            {
                this.logger.LogWarning(ex, $"Failed to describe object at 0x{@object.Address:X}.");
            }

            // NSObject's default implementation.
            return DefaultDescription(className, @object.Address);
        }

        // NSArray's description is a property list in compact form, e.g. (one, "two words", 3).
        private string DescribeArray(Objc2Object array, string className, ref int budget)
        {
            int count = this.GetIndexedVariablesCount(array);

            if (count < 0)
            {
                return DefaultDescription(className, array.Address);
            }

            var sb = new StringBuilder("(");

            for (int i = 0; i < count; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }

                if (budget <= 0)
                {
                    sb.Append("...");
                    break;
                }

                budget--;
                sb.Append(this.DescribeElement(this.GetIndexedVariable(array, i), ref budget));
            }

            return sb.Append(')').ToString();
        }

        // Elements are written as property list items: nested collections inline, everything else as a possibly quoted description.
        private string DescribeElement(Objc2Object element, ref int budget)
        {
            if (element == Objc2Object.Null)
            {
                return "(nil)";
            }

            var className = element.IsSmallObject ? null : this.GetClassName(element);

            if (className != null && this.ArrayClassNames.Contains(className))
            {
                return this.Describe(element, ref budget) ?? "(nil)";
            }

            var description = this.Describe(element, ref budget);
            return description == null ? $"<0x{element.Address:x}>" : QuotePropertyListString(description);
        }

        // -[NSURL description] is the string the URL was created with, followed by " -- " and the base URL if there is one.
        private string DescribeUrl(Objc2Object url, ref int budget)
        {
            // Offset   Field       Type
            // 8        _urlString  NSString *
            // 16       _baseURL    NSURL * (nil unless relative)
            var urlString = this.GetStringValue(this.ReadObject((nuint)this.processMemoryReader.ReadUInt64LittleEndian(url.Address + 8))) ?? string.Empty;
            var description = HidePassword(urlString);

            var baseUrl = this.ReadObject((nuint)this.processMemoryReader.ReadUInt64LittleEndian(url.Address + 16));

            if (baseUrl != Objc2Object.Null)
            {
                description += " -- " + this.Describe(baseUrl, ref budget);
            }

            return description;
        }

        private static string? FormatValue(object? value) => value switch
        {
            int i => i.ToString(CultureInfo.InvariantCulture),
            long l => l.ToString(CultureInfo.InvariantCulture),
            ulong u => u.ToString(CultureInfo.InvariantCulture),
            // NSBoolNumber formats like an int.
            bool b => b ? "1" : "0",
            float f => FormatG(f, 7),
            double d => FormatG(d, 16),
            string s => s,
            // NSDate uses the default calendar format; the target's time zone isn't known, so UTC is assumed.
            DateTimeOffset date => date.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " +0000",
            _ => null
        };

        // Equivalent of printf's %.<precision>g.
        private static string FormatG(double value, int precision)
        {
            if (double.IsNaN(value))
            {
                return "nan";
            }

            if (double.IsInfinity(value))
            {
                return value > 0 ? "inf" : "-inf";
            }

            return value.ToString("G" + precision, CultureInfo.InvariantCulture).Replace('E', 'e');
        }

        private static string DefaultDescription(string className, nuint address) => $"<{className}: 0x{address:x}>";

        // GNUstep hides the password in a URL's description.
        private static string HidePassword(string url)
        {
            int schemeEnd = url.IndexOf("://", StringComparison.Ordinal);

            if (schemeEnd < 0)
            {
                return url;
            }

            int authorityStart = schemeEnd + 3;
            int authorityEnd = url.AsSpan(authorityStart).IndexOfAny('/', '?', '#');
            authorityEnd = authorityEnd < 0 ? url.Length : authorityStart + authorityEnd;

            int at = url.IndexOf('@', authorityStart, authorityEnd - authorityStart);
            int colon = at < 0 ? -1 : url.IndexOf(':', authorityStart, at - authorityStart);

            if (colon < 0 || at == colon + 1)
            {
                return url;
            }

            return string.Concat(url.AsSpan(0, colon + 1), "HIDDEN-PASSWORD", url.AsSpan(at));
        }

        // Property list strings are only left bare if they are non-empty and purely alphanumeric.
        private static string QuotePropertyListString(string value)
        {
            if (value.Length == 0)
            {
                return "\"\"";
            }

            if (value.All(char.IsAsciiLetterOrDigit))
            {
                return value;
            }

            var sb = new StringBuilder(value.Length + 2).Append('"');

            foreach (char c in value)
            {
                switch (c)
                {
                    case '\t':
                    case '\r':
                    case '\n':
                        sb.Append(c);
                        break;
                    case '\a': sb.Append("\\a"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\v': sb.Append("\\v"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    default:
                        if (c >= 0x20 && c < 0x7F)
                        {
                            sb.Append(c);
                        }
                        else if (c < 0x80)
                        {
                            sb.Append('\\').Append(Convert.ToString(c, 8).PadLeft(3, '0'));
                        }
                        else
                        {
                            sb.Append("\\U").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
                        }

                        break;
                }
            }

            return sb.Append('"').ToString();
        }

        private double ReadDouble(nuint address) => BitConverter.UInt64BitsToDouble(this.processMemoryReader.ReadUInt64LittleEndian(address));

        private float ReadFloat(nuint address)
        {
            Span<byte> buffer = stackalloc byte[4];
            this.processMemoryReader.ReadExactly(address, buffer);
            return System.Buffers.Binary.BinaryPrimitives.ReadSingleLittleEndian(buffer);
        }
    }
}
