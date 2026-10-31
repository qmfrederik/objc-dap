namespace Objc.Dap.Runtime
{
    public partial class Objc2Runtime
    {
        // Number of bytes shown in the hex preview.
        private const int DataPreviewLength = 16;

        // See https://github.com/gnustep/libs-base/blob/master/Source/NSData.m
        // NSMutableDataMalloc shares its initial layout with NSDataStatic/NSDataMalloc, and all other
        // concrete classes derive from one of those without adding instance variables.
        private static readonly string[] DataClassNames =
        [
            "NSDataStatic",
            "NSDataEmpty",
            "NSDataMalloc",
            "NSDataWithDeallocator",
            "NSDataWithDeallocatorBlock",
            "NSDataMappedFile",
            "NSDataShared",
            "NSMutableDataMalloc",
            "NSMutableDataWithDeallocator",
            "NSMutableDataWithDeallocatorBlock",
            "NSMutableDataShared",
        ];

        // NSDataStatic / NSDataMalloc / NSMutableDataMalloc layout:
        // Offset   Field               Type
        // 0        isa                 Class
        // 8        length              NSUInteger
        // 16       bytes               void*
        // 24       deallocator         void*
        // NSMutableDataMalloc only:
        // 32       zone                NSZone*
        // 40       capacity            NSUInteger
        // 48       growth              NSUInteger
        private const int DataLengthOffset = 8;
        private const int DataBytesOffset = 16;

        /// <summary>
        /// Gets a short description of an <c>NSData</c> object: its length and a hex preview of the first bytes.
        /// </summary>
        /// <returns>
        /// The description, or <see langword="null"/> if the object is null, a small object or not an <c>NSData</c>.
        /// </returns>
        public string? GetDataValue(Objc2Object @object)
        {
            ArgumentNullException.ThrowIfNull(@object);

            if (@object == Objc2Object.Null || @object.IsSmallObject)
            {
                return null;
            }

            var className = this.GetClassName(@object);

            if (className == null || !DataClassNames.Contains(className))
            {
                return null;
            }

            var length = this.processMemoryReader.ReadUInt64LittleEndian(@object.Address + DataLengthOffset);

            if (length == 0)
            {
                return "<0 bytes>";
            }

            var bytes = this.processMemoryReader.ReadUInt64LittleEndian(@object.Address + DataBytesOffset);

            Span<byte> preview = stackalloc byte[(int)Math.Min(length, DataPreviewLength)];
            this.processMemoryReader.ReadExactly((nuint)bytes, preview);

            var ellipsis = length > DataPreviewLength ? "…" : string.Empty;
            return $"<{length} bytes: {Convert.ToHexStringLower(preview)}{ellipsis}>";
        }
    }
}
