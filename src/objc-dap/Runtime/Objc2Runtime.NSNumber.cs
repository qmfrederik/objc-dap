using System.Buffers.Binary;
using System.Diagnostics;

namespace Objc.Dap.Runtime
{
    public partial class Objc2Runtime
    {
        public const int TINY_VALUE_SHIFT = 3;

        public object? GetNSNumber(Objc2Object @object)
        {
            ArgumentNullException.ThrowIfNull(@object);

            switch(@object.SmallObjectType)
            {
                case SmallObjectType.Int:
                    return GetInt32Value(@object);

                case SmallObjectType.RepeatingDouble:
                case SmallObjectType.ExtendedDouble:
                    return GetDoubleValue(@object);

                case SmallObjectType.Float:
                    return GetFloatValue(@object);
            }

            var @class = this.ReadClass(@object.Isa);
            var className = this.GetClassName(@class);
            return className switch
            {
                "NSIntNumber" => GetInt32Value(@object),
                "NSLongLongNumber" => GetInt64Value(@object),
                "NSUnsignedLongLongNumber" => GetUInt64Value(@object),
                "NSBoolNumber" => GetBoolean(@object),
                "NSDoubleNumber" => GetDoubleValue(@object),
                _ => null
            };
        }

        public int GetInt32Value(Objc2Object @object)
        {
            ArgumentNullException.ThrowIfNull(@object);

            if (@object.SmallObjectType == SmallObjectType.Int)
            {
                var value = (int)((ulong)@object.Address >> TINY_VALUE_SHIFT);
                return value;
            }

            AssertClass(@object, "NSIntNumber");

            // Layout is:
            // Offset   Size    Field
            // 0        8       Class isa
            // 8        4       int value(0 for NO, 1 for YES)

            // Skip past isa, and read the int value
            Span<byte> intValue = stackalloc byte[4];
            this.processMemoryReader.ReadExactly(@object.Address + 8, intValue);

            return BinaryPrimitives.ReadInt32LittleEndian(intValue);
        }

        public long GetInt64Value(Objc2Object @object)
        {
            ArgumentNullException.ThrowIfNull(@object);
            AssertClass(@object, "NSLongLongNumber");

            // Layout is:
            // Offset   Size    Field
            // 0        8       Class isa
            // 8        8       long long value

            // Skip past isa, and read the long long value
            Span<byte> longLongValue = stackalloc byte[8];
            this.processMemoryReader.ReadExactly(@object.Address + 8, longLongValue);
            return BinaryPrimitives.ReadInt64LittleEndian(longLongValue);
        }

        public ulong GetUInt64Value(Objc2Object @object)
        {
            ArgumentNullException.ThrowIfNull(@object);
            AssertClass(@object, "NSUnsignedLongLongNumber");

            // Layout is:
            // Offset   Size    Field
            // 0        8       Class isa
            // 8        8       long long value

            // Skip past isa, and read the long long value
            Span<byte> longLongValue = stackalloc byte[8];
            this.processMemoryReader.ReadExactly(@object.Address + 8, longLongValue);
            return BinaryPrimitives.ReadUInt64LittleEndian(longLongValue);
        }

        public float GetFloatValue(Objc2Object @object)
        {
            ArgumentNullException.ThrowIfNull(@object);

            if (@object.SmallObjectType == SmallObjectType.Float)
            {
                ulong mask = @object.Address & 0x8UL;
                ulong bits = (@object.Address & ~0x7UL) | (mask >> 1) | (mask >> 2) | (mask >> 3);
                return (float)BitConverter.UInt64BitsToDouble(bits);
            }

            throw new NotImplementedException();
        }

        public double GetDoubleValue(Objc2Object @object)
        {
            ArgumentNullException.ThrowIfNull(@object);

            if (@object.SmallObjectType == SmallObjectType.RepeatingDouble)
            {
                // Original low 3 bits equal bits 5:3, which the tag overwrote.
                ulong bits = (@object.Address & ~0x7UL) | ((@object.Address & 0x38UL) >> 3);
                return BitConverter.UInt64BitsToDouble(bits);
            }
            else if (@object.SmallObjectType == SmallObjectType.ExtendedDouble)
            {
                // Boxing accepts only doubles whose low 3 bits all equal bit 3, so replicate bit 3.
                ulong bit3 = @object.Address & 0x8UL;
                ulong bits = (@object.Address & ~0x7UL) | (bit3 >> 1) | (bit3 >> 2) | (bit3 >> 3);
                return BitConverter.UInt64BitsToDouble(bits);
            }

            AssertClass(@object, "NSDoubleNumber");
            // Layout is:
            // Offset   Size    Field
            // 0        8       Class isa
            // 8        8       double value (IEEE-754)

            // Skip past isa, and read the double value
            Span<byte> doubleValue = stackalloc byte[8];
            this.processMemoryReader.ReadExactly(@object.Address + 8, doubleValue);
            return BinaryPrimitives.ReadDoubleLittleEndian(doubleValue);
        }

        public bool GetBoolean(Objc2Object @object)
        {
            ArgumentNullException.ThrowIfNull(@object);
            AssertClass(@object, "NSBoolNumber");

            // Layout is:
            // Offset   Size    Field
            // 0        8       Class isa
            // 8        4       int value(0 for NO, 1 for YES)

            // Skip past isa, and read the int value
            Span<byte> intValue = stackalloc byte[4];
            this.processMemoryReader.ReadExactly(@object.Address + 8, intValue);
            return BinaryPrimitives.ReadInt32LittleEndian(intValue) != 0;
        }

        [Conditional("DEBUG")]
        private void AssertClass(Objc2Object @object, string className)
        {
            var @class = this.ReadClass(@object.Isa);
            var value = this.GetClassName(@class);
            Debug.Assert(value == className, $"Expected class name to be {className}, but got {value}");

        }
    }
}
