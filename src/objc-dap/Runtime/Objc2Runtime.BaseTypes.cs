using System.Buffers.Binary;
using System.Drawing;

namespace Objc.Dap.Runtime
{
    public partial class Objc2Runtime
    {
        private static readonly DateTimeOffset Epoch = new DateTimeOffset(2001, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public DateTimeOffset GetDateValue(Objc2Object @object)
        {
            ArgumentNullException.ThrowIfNull(@object);

            if (@object.SmallObjectType == SmallObjectType.Date)
            {
                var p = @object.Address;
                ulong fraction = (p >> 3) & ((1UL << 52) - 1);
                long exp8 = (sbyte)((p >> 55) & 0xFF);       // sign-extend 8-bit exponent
                ulong biased = (ulong)(exp8 + 0x3EF) & 0x7FF; // back to 11 bits
                ulong sign = p >> 63;

                double seconds = BitConverter.UInt64BitsToDouble((sign << 63) | (biased << 52) | fraction);
                return Epoch.AddSeconds(seconds);
            }

            throw new NotSupportedException();
        }

        public Uri? GetUriValue(Objc2Object @object)
        {
            ArgumentNullException.ThrowIfNull(@object);
            this.AssertClass(@object, "NSURL");

            // Offset   Field       Type
            // 0        isa	        Class
            // 8        _urlString  NSString * (the string as given, which may be relative)
            // 16       _baseURL    NSURL * (nil unless relative)
            // 24       _clients    void * (NSMapTable, usually NULL)
            // 32       _data       parsedURL * (malloc'd)

            // Read the _urlString field (at offset 8) and convert it to a string
            var urlStringAddress = this.processMemoryReader.ReadUInt64LittleEndian(@object.Address + 8);
            var urlStringObject = this.ReadObject((nuint)urlStringAddress);
            var urlString = this.GetStringValue(urlStringObject);

            return new Uri(urlString!);
        }

        public PointF GetPointValue(Objc2Object @object)
        {
            ArgumentNullException.ThrowIfNull(@object);
            this.AssertClass(@object, "GSPointValue");

            // Offset   Field       Type
            // 0        isa         Class
            // 8        data.x      CGFloat (8-byte double)
            // 16       data.y      CGFloat (8-byte double)

            Span<byte> xy = stackalloc byte[16];
            this.processMemoryReader.ReadExactly(@object.Address + 8, xy);
            var x = BinaryPrimitives.ReadDoubleLittleEndian(xy.Slice(0, 8));
            var y = BinaryPrimitives.ReadDoubleLittleEndian(xy.Slice(8, 8));

            return new PointF((float)x, (float)y);
        }

        public SizeF GetSizeValue(Objc2Object @object)
        {
            ArgumentNullException.ThrowIfNull(@object);
            this.AssertClass(@object, "GSSizeValue");

            // Offset   Field       Type
            // 0        isa         Class
            // 8        data.width  CGFloat (8-byte double)
            // 16       data.height CGFloat (8-byte double)

            Span<byte> wh = stackalloc byte[16];
            this.processMemoryReader.ReadExactly(@object.Address + 8, wh);
            var width = BinaryPrimitives.ReadDoubleLittleEndian(wh.Slice(0, 8));
            var height = BinaryPrimitives.ReadDoubleLittleEndian(wh.Slice(8, 8));
            return new SizeF((float)width, (float)height);
        }

        public RectangleF GetRectValue(Objc2Object @object)
        {
            ArgumentNullException.ThrowIfNull(@object);
            this.AssertClass(@object, "GSRectValue");

            // Offset   Field       Type
            // 0        isa         Class
            // 8        data.origin.x   CGFloat (8-byte double)
            // 16       data.origin.y   CGFloat (8-byte double)
            // 24       data.size.width  CGFloat (8-byte double)
            // 32       data.size.height CGFloat (8-byte double)

            Span<byte> xywh = stackalloc byte[32];
            this.processMemoryReader.ReadExactly(@object.Address + 8, xywh);
            var x = BinaryPrimitives.ReadDoubleLittleEndian(xywh.Slice(0, 8));
            var y = BinaryPrimitives.ReadDoubleLittleEndian(xywh.Slice(8, 8));
            var width = BinaryPrimitives.ReadDoubleLittleEndian(xywh.Slice(16, 8));
            var height = BinaryPrimitives.ReadDoubleLittleEndian(xywh.Slice(24, 8));
            return new RectangleF((float)x, (float)y, (float)width, (float)height);
        }
    }
}
