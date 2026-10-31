namespace Objc.Dap.Runtime
{
    public partial class Objc2Runtime
    {
        private readonly string[] ArrayClassNames =
        [
            "GSInlineArray",
            "GSMutableArray",
        ];

        public int GetIndexedVariablesCount(Objc2Object @object)
        {
            ArgumentNullException.ThrowIfNull(@object);

            if (@object == Objc2Object.Null || @object.IsSmallObject)
            {
                return -1;
            }

            var className = this.GetClassName(@object);

            // Sets are exposed as indexed variables too.
            if (this.IsSet(className))
            {
                return this.GetSetCount(@object);
            }

            if (!this.ArrayClassNames.Contains(className))
            {
                return -1;
            }

            // GSArray Layout:
            // Offset   Field               Type
            // 0        isa	                Class
            // 8        *_contents_array    id 
            // 16       _count              NSUInteger 
            // 24       id[0] … id[count-1]

            // GSMutableArray Layout:
            // Offset   Field               Type
            // 0        isa	                Class
            // 8        *_contents_array    id
            // 16       _count              NSUInteger
            // 24       _capacity           NSUInteger
            // 32       _grow_factor        int
            // 40       _version            unsigned long   // newer releases only, used for mutation checks in fast enumeration

            var count = this.processMemoryReader.ReadUInt64LittleEndian(@object.Address + 16);
            return (int)count;
        }

        public Objc2Object GetIndexedVariable(Objc2Object @object, int index)
        {
            ArgumentNullException.ThrowIfNull(@object);

            // This will do all the validation
            var count = this.GetIndexedVariablesCount(@object);

            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, count, nameof(index));

            if (this.IsSet(this.GetClassName(@object)))
            {
                return this.GetSetMember(@object, index);
            }

            var contentsArray = this.processMemoryReader.ReadUInt64LittleEndian(@object.Address + 8);
            var value = this.processMemoryReader.ReadUInt64LittleEndian((nuint)(contentsArray + (ulong)(index * 8)));
            return this.ReadObject((nuint)value);
        }
    }
}
