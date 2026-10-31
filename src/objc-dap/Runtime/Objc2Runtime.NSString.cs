using System.Buffers.Binary;
using System.Reflection;
using System.Text;

namespace Objc.Dap.Runtime
{
    public partial class Objc2Runtime
    {
        // The concrete GSString classes share the initial layout of GSString (see GSPrivate.h and GSString.m),
        // so they can all be read the same way.  The sub strings point into the buffer of their parent.
        private static readonly string[] GSStringClassNames =
        [
            "GSCInlineString",
            "GSCBufferString",
            "GSCSubString",
            "GSUInlineString",
            "GSUnicodeBufferString",
            "GSUnicodeSubString",
            "GSMutableString",
        ];

        public string? GetStringValue(Objc2Object @object)
        {
            ArgumentNullException.ThrowIfNull(@object);

            if (@object.SmallObjectType == SmallObjectType.String)
            {
                return this.GetTinyStringValue(@object);
            }

            var @class = this.ReadClass(@object.Isa);
            var className = this.GetClassName(@class);

            if (className != null && GSStringClassNames.Contains(className))
            {
                return this.GetGSCInlineStringValue(@object);
            }

            return className switch
            {
                "NSConstantString" => this.GetNSConstantStringValue(@object),
                _ => null
            };
        }
        
        public string? GetTinyStringValue(Objc2Object @object)
        {
            ArgumentNullException.ThrowIfNull(@object);

            const int MaxLength = 9;  // a 9th byte is an implicit NULL

            int length = (int)((@object.Address >> 3) & 0x1f);
            if (length > MaxLength)
            {
                return null;
            }

            var sb = new StringBuilder(length);
            for (int i = 0; i < length; i++)
                sb.Append((char)((@object.Address >> (57 - 7 * i)) & 0x7f));

            return sb.ToString();
        }

        public string GetNSConstantStringValue(Objc2Object @object)
        {
            ArgumentNullException.ThrowIfNull(@object);

            // See https://github.com/gnustep/libs-base/blob/master/Headers/Foundation/NSString.h#L1190
            //
            // The NSConstantString layout is as follows:
            // struct {
            //    Class     isa;      // NSConstantString
            //    uint32_t  flags;    // string encoding:
            //                        // 0. ASCII (UTF-8 using only 7-bit characters)
            //                        // 1. UTF-8
            //                        // 2. UTF-16
            //                        // 3. UTF-32
            //    uint32_t  length;   // length in UTF-16 code units (characters)
            //    uint32_t  size;     // size of the data in bytes
            //    uint32_t  hash;     // precomputed hash
            //    const char *data;   // pointer to the characters
            // };

            // Skip past isa, and read the header
            Span<byte> header = stackalloc byte[16];
            this.processMemoryReader.ReadExactly(@object.Address + 8, header);

            uint flags = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(0, 4));
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(8, 4));

            // Dereference the data pointer
            nuint dataPtr = (nuint)this.processMemoryReader.ReadUInt64LittleEndian(@object.Address + 24);

            using var owner = this.memoryPool.Rent((int)size);
            var data = owner.Memory.Span.Slice(0, (int)size);
            this.processMemoryReader.ReadExactly(dataPtr, data);

            var encoding = flags switch
            {
#pragma warning disable SYSLIB0001 // Type or member is obsolete
                0 => Encoding.UTF7,
#pragma warning restore SYSLIB0001 // Type or member is obsolete
                1 => Encoding.UTF8,
                2 => Encoding.Unicode,
                3 => Encoding.UTF32,
                _ => throw new NotSupportedException($"Unsupported string encoding: {flags}")
            };

            var value = encoding.GetString(data);
            return value;
        }

        public string GetGSCInlineStringValue(Objc2Object @object)
        {
            ArgumentNullException.ThrowIfNull(@object);

            // https://github.com/gnustep/libs-base/blob/master/Source/GSPrivate.h#L236
            // Layout:
            // Class        isa
            // GSCharPtr    _contents;
            // unsigned int _count;
            // struct {
            //   unsigned int wide: 1;	// 16-bit characters in string?
            //   unsigned int owned: 1;	// Set if the instance owns _contents buffer
            //   unsigned int unused: 2;
            //   unsigned int hash: 28;
            // } _flags;

            // Skip past isa and _contents, and read the header
            Span<byte> header = stackalloc byte[8];
            this.processMemoryReader.ReadExactly(@object.Address + 16, header);

            uint count = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(0, 4));
            uint flags = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(4, 4));

            // Dereference the data pointer
            nuint contentsPtr = (nuint)this.processMemoryReader.ReadUInt64LittleEndian(@object.Address + 8);

            var wide = (flags & 1) != 0;

            // The count is in characters, so 16-bit strings take two bytes per character.
            var byteCount = wide ? checked((int)count * 2) : (int)count;
            using var owner = this.memoryPool.Rent(byteCount);
            var data = owner.Memory.Span.Slice(0, byteCount);
            this.processMemoryReader.ReadExactly(contentsPtr, data);

            var encoding = wide ? Encoding.Unicode : Encoding.UTF8;
            var value = encoding.GetString(data);
            return value;
        }
    }
}
