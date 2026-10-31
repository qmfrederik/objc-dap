using System.Buffers.Binary;

namespace Objc.Dap.Runtime
{
    public partial class Objc2Runtime
    {
        private static readonly string[] SetClassNames =
        [
            "GSSet",
            "GSMutableSet",
        ];

        // GSSet / GSMutableSet layout (see Source/GSSet.m and Headers/GNUstepBase/GSIMap.h).
        // GSSet.m defines GSI_MAP_HAS_VALUE as 0, so the map nodes only hold a key.
        // Offset   Field               Type
        // 0        isa                 Class
        // 8        map.zone            NSZone*
        // 16       map.nodeCount       uintptr_t   // number of members
        // 24       map.bucketCount     uintptr_t
        // 32       map.buckets         GSIMapBucket
        // 40       map.freeNodes       GSIMapNode
        // 48       map.chunkCount      uintptr_t
        // 56       map.nodeChunks      GSIMapNode*
        // 64       map.increment       uintptr_t
        // 72       _version            unsigned long   // GSMutableSet only
        //
        // GSIMapBucket (16 bytes): nodeCount (uintptr_t), firstNode (GSIMapNode)
        // GSIMapNode (16 bytes):   nextInBucket (GSIMapNode), key (id)
        private const int SetNodeCountOffset = 16;
        private const int SetBucketCountOffset = 24;
        private const int SetBucketsOffset = 32;
        private const int SetBucketSize = 16;
        private const int SetNodeSize = 16;
        private const int SetNodeKeyOffset = 8;

        private bool IsSet(string? className) => className != null && SetClassNames.Contains(className);

        private int GetSetCount(Objc2Object @object)
        {
            var count = this.processMemoryReader.ReadUInt64LittleEndian(@object.Address + SetNodeCountOffset);
            return (int)count;
        }

        /// <summary>
        /// Gets the set member at position <paramref name="index"/> in the hash table's iteration order.
        /// </summary>
        private Objc2Object GetSetMember(Objc2Object @object, int index)
        {
            var bucketCount = this.processMemoryReader.ReadUInt64LittleEndian(@object.Address + SetBucketCountOffset);
            var buckets = this.processMemoryReader.ReadUInt64LittleEndian(@object.Address + SetBucketsOffset);

            Span<byte> bucket = stackalloc byte[SetBucketSize];
            Span<byte> node = stackalloc byte[SetNodeSize];
            ulong remaining = (ulong)index;

            for (ulong i = 0; i < bucketCount; i++)
            {
                this.processMemoryReader.ReadExactly((nuint)(buckets + i * SetBucketSize), bucket);
                var bucketNodeCount = BinaryPrimitives.ReadUInt64LittleEndian(bucket);

                // Skip the whole bucket if the requested member isn't in it.
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
                        return this.ReadObject((nuint)BinaryPrimitives.ReadUInt64LittleEndian(node.Slice(SetNodeKeyOffset)));
                    }

                    nodeAddress = BinaryPrimitives.ReadUInt64LittleEndian(node);
                }
            }

            throw new InvalidOperationException($"Could not find member {index} in the set at 0x{@object.Address:X}.");
        }
    }
}
