using System.Buffers.Binary;
using System.ComponentModel;
using System.Text;

namespace Objc.Dap.Runtime
{
    public partial class Objc2Runtime
    {
        // objc_class (libobjc2 v2.2.1, class.h):
        // Offset (LP64)  Offset (LLP64)  Field               Type
        // 0              0               isa                 Class
        // 8              8               super_class         Class
        // 16             16              name                const char *
        // 24             24              version             long
        // 32             28              info                unsigned long
        // 40             32              instance_size       long    // size of an instance, including superclass ivars, once the class is registered
        // 48             40              ivars               struct objc_ivar_list *
        // `long` is 4 bytes on Windows (LLP64) and 8 on other 64-bit platforms, which shifts the fields after `version`.
        //
        // objc_ivar_list (ivar.h):
        // Offset   Field               Type
        // 0        count               int
        // 8        size                size_t                  // stride of ivar_list entries
        // 16       ivar_list[count]    struct objc_ivar
        //
        // objc_ivar (ivar.h):
        // Offset   Field               Type
        // 0        name                const char *
        // 8        type                const char *            // type encoding
        // 16       offset              int *                   // points to the offset from the object pointer, resolved by the runtime
        // 24       size                uint32_t
        // 28       flags               uint32_t
        private static readonly int LongSize = OperatingSystem.IsWindows() ? sizeof(int) : sizeof(long);
        private static readonly int ClassInstanceSizeOffset = (3 * IntPtr.Size) + (2 * LongSize);
        private static readonly int ClassIvarsOffset = (ClassInstanceSizeOffset + LongSize + IntPtr.Size - 1) & ~(IntPtr.Size - 1);
        private const int IvarListHeaderSize = 16;
        private const int IvarSize = 32;
        private const int IvarTypeOffset = 8;
        private const int IvarOffsetPointerOffset = 16;
        private const int IvarSizeFieldOffset = 24;
        private const int MaxInstanceVariablesPerClass = 1024;
        private const int MaxIvarStride = 1024;
        private const int MaxIvarNameLength = 128;
        private const int MaxTypeEncodingLength = 512;
        private const int MaxRawInstanceVariableBytes = 64;

        /// <summary>
        /// Gets the instance variables of <paramref name="object"/>, including the ones declared by its superclasses,
        /// ordered from the root class down.
        /// </summary>
        /// <returns>
        /// The instance variables, or an empty list for null and small objects.
        /// </returns>
        /// <exception cref="InvalidOperationException">The class metadata could not be read or is not plausible.</exception>
        public IReadOnlyList<Objc2InstanceVariable> GetInstanceVariables(Objc2Object @object)
        {
            ArgumentNullException.ThrowIfNull(@object);

            if (@object == Objc2Object.Null || @object.IsSmallObject)
            {
                return [];
            }

            try
            {
                return this.ReadInstanceVariables(@object.Isa);
            }
            catch (Win32Exception ex)
            {
                throw new InvalidOperationException($"Could not read the instance variables of the object at 0x{@object.Address:X}.", ex);
            }
        }

        private List<Objc2InstanceVariable> ReadInstanceVariables(nuint isa)
        {
            var chain = new List<nuint>();

            for (nuint current = isa; current != 0; current = this.ReadClass(current).SuperClass)
            {
                if (chain.Count >= MaxClassChainDepth)
                {
                    throw new InvalidOperationException($"The class at 0x{isa:X} has a superclass chain deeper than {MaxClassChainDepth}.");
                }

                chain.Add(current);
            }

            var instanceSize = this.ReadInstanceSize(isa);
            var instanceVariables = new List<Objc2InstanceVariable>();

            for (int i = chain.Count - 1; i >= 0; i--)
            {
                this.ReadClassInstanceVariables(chain[i], instanceSize, instanceVariables);
            }

            return instanceVariables;
        }

        private long ReadInstanceSize(nuint @class)
        {
            Span<byte> buffer = stackalloc byte[sizeof(long)];
            var instanceSize = buffer.Slice(0, LongSize);
            this.processMemoryReader.ReadExactly(@class + (nuint)ClassInstanceSizeOffset, instanceSize);

            return LongSize == sizeof(int)
                ? BinaryPrimitives.ReadInt32LittleEndian(instanceSize)
                : BinaryPrimitives.ReadInt64LittleEndian(instanceSize);
        }

        private void ReadClassInstanceVariables(nuint @class, long instanceSize, List<Objc2InstanceVariable> instanceVariables)
        {
            var ivarList = this.processMemoryReader.ReadUInt64LittleEndian(@class + (nuint)ClassIvarsOffset);

            if (ivarList == 0)
            {
                return;
            }

            Span<byte> header = stackalloc byte[IvarListHeaderSize];
            this.processMemoryReader.ReadExactly((nuint)ivarList, header);
            var count = BinaryPrimitives.ReadInt32LittleEndian(header);
            var stride = BinaryPrimitives.ReadUInt64LittleEndian(header.Slice(8));

            if (count < 0 || count > MaxInstanceVariablesPerClass || (count > 0 && (stride < IvarSize || stride > MaxIvarStride)))
            {
                throw new InvalidOperationException($"The instance variable list at 0x{ivarList:X} is not valid (count {count}, entry size {stride}).");
            }

            Span<byte> ivar = stackalloc byte[IvarSize];
            Span<byte> offsetBytes = stackalloc byte[sizeof(int)];

            for (int i = 0; i < count; i++)
            {
                var ivarAddress = ivarList + IvarListHeaderSize + (ulong)i * stride;
                this.processMemoryReader.ReadExactly((nuint)ivarAddress, ivar);

                var name = BinaryPrimitives.ReadUInt64LittleEndian(ivar);
                var type = BinaryPrimitives.ReadUInt64LittleEndian(ivar.Slice(IvarTypeOffset));
                var offsetAddress = BinaryPrimitives.ReadUInt64LittleEndian(ivar.Slice(IvarOffsetPointerOffset));
                var size = BinaryPrimitives.ReadUInt32LittleEndian(ivar.Slice(IvarSizeFieldOffset));

                if (name == 0 || type == 0 || offsetAddress == 0)
                {
                    throw new InvalidOperationException($"The instance variable at 0x{ivarAddress:X} has a null name, type or offset.");
                }

                this.processMemoryReader.ReadExactly((nuint)offsetAddress, offsetBytes);
                var offset = BinaryPrimitives.ReadInt32LittleEndian(offsetBytes);

                if (offset < 0 || offset + (long)size > instanceSize)
                {
                    throw new InvalidOperationException($"The instance variable at 0x{ivarAddress:X} has offset {offset} and size {size}, which does not fit in an instance of {instanceSize} bytes.");
                }

                instanceVariables.Add(new Objc2InstanceVariable()
                {
                    Name = this.ReadCString((nuint)name, MaxIvarNameLength),
                    TypeEncoding = this.ReadCString((nuint)type, MaxTypeEncodingLength),
                    Offset = offset,
                    Size = size,
                });
            }
        }

        /// <summary>
        /// Reads the value of <paramref name="instanceVariable"/> in <paramref name="object"/>, according to its type encoding.
        /// </summary>
        /// <returns>
        /// A boxed <see cref="sbyte"/>, <see cref="byte"/>, <see cref="bool"/>, <see cref="short"/>, <see cref="ushort"/>,
        /// <see cref="int"/>, <see cref="uint"/>, <see cref="long"/>, <see cref="ulong"/>, <see cref="float"/> or <see cref="double"/>
        /// for scalars; an <see cref="Objc2Object"/> for objects (<see cref="Objc2Object.Null"/> if the pointer isn't a valid object);
        /// an <see cref="Objc2Class"/> for classes; a <see cref="nuint"/> for other pointers; <see langword="null"/> for nil objects and classes;
        /// and the leading bytes (at most 64) for anything else, such as structs.
        /// </returns>
        /// <exception cref="InvalidOperationException">The value could not be read.</exception>
        public object? GetInstanceVariableValue(Objc2Object @object, Objc2InstanceVariable instanceVariable)
        {
            ArgumentNullException.ThrowIfNull(@object);
            ArgumentNullException.ThrowIfNull(instanceVariable);

            if (@object == Objc2Object.Null || @object.IsSmallObject)
            {
                throw new ArgumentException("The object must be a valid, non-small object.", nameof(@object));
            }

            try
            {
                return this.ReadInstanceVariableValue(@object.Address + (nuint)instanceVariable.Offset, instanceVariable);
            }
            catch (Win32Exception ex)
            {
                throw new InvalidOperationException($"Could not read the instance variable '{instanceVariable.Name}' of the object at 0x{@object.Address:X}.", ex);
            }
        }

        private object? ReadInstanceVariableValue(nuint address, Objc2InstanceVariable instanceVariable)
        {
            var kind = instanceVariable.EncodingKind;

            // Type encodings give 'l' and 'L' a fixed 32-bit size, unlike the C types.
            int scalarSize = kind switch
            {
                'c' or 'C' or 'B' => 1,
                's' or 'S' => 2,
                'i' or 'I' or 'l' or 'L' or 'f' => 4,
                'q' or 'Q' or 'd' => 8,
                '@' or '#' or ':' or '*' or '^' => IntPtr.Size,
                _ => 0,
            };

            if (scalarSize == 0 || instanceVariable.Size != scalarSize)
            {
                var raw = new byte[Math.Min(instanceVariable.Size, MaxRawInstanceVariableBytes)];
                this.processMemoryReader.ReadExactly(address, raw);
                return raw;
            }

            Span<byte> buffer = stackalloc byte[8];
            var value = buffer.Slice(0, scalarSize);
            this.processMemoryReader.ReadExactly(address, value);

            switch (kind)
            {
                case 'c': return (sbyte)value[0];
                case 'C': return value[0];
                case 'B': return value[0] != 0;
                case 's': return BinaryPrimitives.ReadInt16LittleEndian(value);
                case 'S': return BinaryPrimitives.ReadUInt16LittleEndian(value);
                case 'i':
                case 'l': return BinaryPrimitives.ReadInt32LittleEndian(value);
                case 'I':
                case 'L': return BinaryPrimitives.ReadUInt32LittleEndian(value);
                case 'f': return BinaryPrimitives.ReadSingleLittleEndian(value);
                case 'q': return BinaryPrimitives.ReadInt64LittleEndian(value);
                case 'Q': return BinaryPrimitives.ReadUInt64LittleEndian(value);
                case 'd': return BinaryPrimitives.ReadDoubleLittleEndian(value);
            }

            var pointer = (nuint)BinaryPrimitives.ReadUInt64LittleEndian(value);

            switch (kind)
            {
                case '@': return pointer == 0 ? null : this.ReadObject(pointer);
                case '#': return pointer == 0 ? null : this.ReadClass(pointer);
                default: return pointer;
            }
        }

        private string ReadCString(nuint address, int maxLength)
        {
            Span<byte> buffer = stackalloc byte[maxLength];
            int read = this.processMemoryReader.Read(address, buffer);
            int terminator = buffer.Slice(0, read).IndexOf((byte)0);

            if (terminator < 0)
            {
                throw new InvalidOperationException($"The string at 0x{address:X} is not terminated within {read} readable bytes (limit {maxLength}).");
            }

            return Encoding.UTF8.GetString(buffer.Slice(0, terminator));
        }
    }
}
