using System.Buffers.Binary;
using System.Globalization;

namespace Objc.Dap.Runtime
{
    public partial class Objc2Runtime
    {
        private static readonly string[] DictionaryClassNames =
        [
            "GSDictionary",
            "GSMutableDictionary",
        ];

        // GSDictionary / GSMutableDictionary layout (see Source/GSDictionary.m and Headers/GNUstepBase/GSIMap.h):
        // Offset   Field               Type
        // 0        isa                 Class
        // 8        map.zone            NSZone*
        // 16       map.nodeCount       uintptr_t   // number of entries
        // 24       map.bucketCount     uintptr_t
        // 32       map.buckets         GSIMapBucket
        // 40       map.freeNodes       GSIMapNode
        // 48       map.chunkCount      uintptr_t
        // 56       map.nodeChunks      GSIMapNode*
        // 64       map.increment       uintptr_t
        // 72       _version            unsigned long   // GSMutableDictionary only
        //
        // GSIMapBucket (16 bytes): nodeCount (uintptr_t), firstNode (GSIMapNode)
        // GSIMapNode (24 bytes):   nextInBucket (GSIMapNode), key (id), value (id)
        private const int DictionaryNodeCountOffset = 16;
        private const int DictionaryBucketCountOffset = 24;
        private const int DictionaryBucketsOffset = 32;
        private const int DictionaryBucketSize = 16;
        private const int DictionaryNodeSize = 24;
        private const int DictionaryNodeKeyOffset = 8;
        private const int DictionaryNodeValueOffset = 16;

        public int GetNamedVariablesCount(Objc2Object @object)
        {
            ArgumentNullException.ThrowIfNull(@object);

            if (@object == Objc2Object.Null || @object.IsSmallObject)
            {
                return -1;
            }

            var className = this.GetClassName(@object);

            if (className == null || !DictionaryClassNames.Contains(className))
            {
                return -1;
            }

            var count = this.processMemoryReader.ReadUInt64LittleEndian(@object.Address + DictionaryNodeCountOffset);
            return (int)count;
        }

        /// <summary>
        /// Gets the dictionary entry at position <paramref name="index"/> in the hash table's iteration order.
        /// </summary>
        /// <returns>
        /// A string representation of the key, and the value object.
        /// </returns>
        public (string, Objc2Object) GetNamedVariable(Objc2Object @object, int index)
        {
            ArgumentNullException.ThrowIfNull(@object);
            ArgumentOutOfRangeException.ThrowIfNegative(index);

            // This will do all the validation
            var count = this.GetNamedVariablesCount(@object);

            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, count, nameof(index));

            var bucketCount = this.processMemoryReader.ReadUInt64LittleEndian(@object.Address + DictionaryBucketCountOffset);
            var buckets = this.processMemoryReader.ReadUInt64LittleEndian(@object.Address + DictionaryBucketsOffset);

            Span<byte> bucket = stackalloc byte[DictionaryBucketSize];
            Span<byte> node = stackalloc byte[DictionaryNodeSize];
            ulong remaining = (ulong)index;

            for (ulong i = 0; i < bucketCount; i++)
            {
                this.processMemoryReader.ReadExactly((nuint)(buckets + i * DictionaryBucketSize), bucket);
                var bucketNodeCount = BinaryPrimitives.ReadUInt64LittleEndian(bucket);

                // Skip the whole bucket if the requested entry isn't in it.
                if (bucketNodeCount <= remaining)
                {
                    remaining -= bucketNodeCount;
                    continue;
                }

                var nodeAddress = BinaryPrimitives.ReadUInt64LittleEndian(bucket.Slice(8));

                // Bounded by remaining, so a corrupt list can't loop forever.
                for (ulong j = 0; j <= remaining; j++)
                {
                    this.processMemoryReader.ReadExactly((nuint)nodeAddress, node);

                    if (j == remaining)
                    {
                        var key = this.ReadObject((nuint)BinaryPrimitives.ReadUInt64LittleEndian(node.Slice(DictionaryNodeKeyOffset)));
                        var value = this.ReadObject((nuint)BinaryPrimitives.ReadUInt64LittleEndian(node.Slice(DictionaryNodeValueOffset)));
                        return (this.GetKeyDisplayName(key), value);
                    }

                    nodeAddress = BinaryPrimitives.ReadUInt64LittleEndian(node);
                }
            }

            throw new InvalidOperationException($"Could not find entry {index} in the dictionary at 0x{@object.Address:X}.");
        }

        private string GetKeyDisplayName(Objc2Object key)
        {
            if (key == Objc2Object.Null)
            {
                return "nil";
            }

            var value = this.GetObjectValue(key);

            if (value != null)
            {
                return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            }

            return key.IsSmallObject
                ? $"0x{key.Address:X}"
                : $"<{this.GetClassName(key)}: 0x{key.Address:X}>";
        }
    }
}
