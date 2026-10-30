using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Buffers.Binary;

namespace Objc.Dap.Runtime
{
    public partial class Objc2Runtime
    {
        public const uint OBJC_SMALL_OBJECT_MASK = 0x7;

        private readonly ProcessMemoryReader processMemoryReader;
        private readonly ILogger<Objc2Runtime> logger;
        private readonly MemoryPool<byte> memoryPool = MemoryPool<byte>.Shared;

        private readonly Dictionary<string, Func<Objc2Object, object?>> readers = new Dictionary<string, Func<Objc2Object, object?>>();

        public Objc2Runtime(ProcessMemoryReader processMemoryReader, ILogger<Objc2Runtime> logger)
        {
            this.processMemoryReader = processMemoryReader ?? throw new ArgumentNullException(nameof(processMemoryReader));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

            this.readers.Add("NSIntNumber", (obj) => GetInt32Value(obj));
            this.readers.Add("NSLongLongNumber", (obj) => GetInt64Value(obj));
            this.readers.Add("NSUnsignedLongLongNumber", (obj) => GetUInt64Value(obj));
            this.readers.Add("NSBoolNumber", (obj) => GetBoolean(obj));
            this.readers.Add("NSDoubleNumber", (obj) => GetDoubleValue(obj));
            this.readers.Add("NSString", this.GetStringValue);
            this.readers.Add("NSConstantString", this.GetNSConstantStringValue);
            this.readers.Add("GSCInlineString", this.GetGSCInlineStringValue);
            this.readers.Add("GSMutableString", this.GetGSCInlineStringValue);
            this.readers.Add("NSURL", this.GetUriValue);
            this.readers.Add("GSPointValue", (obj) => this.GetPointValue(obj));
            this.readers.Add("GSSizeValue", (obj) => this.GetSizeValue(obj));
            this.readers.Add("GSRectValue", (obj) => this.GetRectValue(obj));
        }

        public void Open(int processId) => this.processMemoryReader.Open(processId);

        public object? GetObjectValue(Objc2Object @object)
        {
            if (@object == Objc2Object.Null)
            {
                return null;
            }

            switch (@object.SmallObjectType)
            {
                case SmallObjectType.Int:
                    return this.GetInt32Value(@object);

                case SmallObjectType.ExtendedDouble:
                    return this.GetDoubleValue(@object);

                case SmallObjectType.RepeatingDouble:
                    return this.GetDoubleValue(@object);

                case SmallObjectType.String:
                    return this.GetStringValue(@object);

                case SmallObjectType.Float:
                    return this.GetFloatValue(@object);

                case SmallObjectType.Date:
                    return this.GetDateValue(@object);
            }

            var @class = this.ReadClass(@object.Isa);
            var className = this.GetClassName(@class);

            if (className == null)
            {
                return null;
            }

            if (this.readers.ContainsKey(className))
            {
                return this.readers[className](@object);
            }

            return null;
        }

        public Objc2Object ReadObject(nuint address)
        {
            // If the value is a small object, return immediately.
            if ((address & OBJC_SMALL_OBJECT_MASK) != 0)
            {
                return new Objc2Object()
                {
                    Address = address,
                    Isa = 0 // Small objects don't have an isa pointer
                };
            }

            // The objc_object layout is as follows:
            // typedef struct objc_object
            // {
            //    Class isa;
            // } *id

            // Dereference the pointer to get the isa pointer
            if (!this.processMemoryReader.IsDereferencableMemory(address))
            {
                this.logger.LogWarning($"Address 0x{address:X} is not dereferencable.");
                return Objc2Object.Null;
            }

            Span<byte> buffer = stackalloc byte[8]; // Assuming 64-bit pointers
            int read = this.processMemoryReader.Read(address, buffer);
            if (read != buffer.Length)
            {
                this.logger.LogWarning($"Failed to read memory at address 0x{address:X}. Read {read} bytes.");
                return Objc2Object.Null;
            }

            nuint isa = (nuint)BinaryPrimitives.ReadUInt64LittleEndian(buffer);
            if (!this.ValidateClassHierarchy(isa))
            {
                this.logger.LogWarning($"Address 0x{address:X} has an isa (0x{isa:X}) that is not a valid class.");
                return Objc2Object.Null;
            }

            return new Objc2Object()
            {
                Address = address,
                Isa = isa
            };
        }

        public Objc2Class ReadClass(nuint isa)
        {
            // The objc_class layout is as follows:
            // struct objc_class
            // {
            //   Class isa; // metaclass [8 bytes]
            //   Class super_class;      [8 bytes]
            //   const char *name;       [8 bytes]
            //   long version;           [8 bytes]
            //   unsigned long info;     [8 bytes]
            // }
            Span<byte> isaBuffer = stackalloc byte[40];
            int read = this.processMemoryReader.Read((nuint)isa, isaBuffer);

            if (read != isaBuffer.Length)
            {
                return Objc2Class.Null;
            }

            nuint superClass = (nuint)BinaryPrimitives.ReadUInt64LittleEndian(isaBuffer.Slice(8, 8));
            nuint name = (nuint)BinaryPrimitives.ReadUInt64LittleEndian(isaBuffer.Slice(16, 8));
            long version = (long)BinaryPrimitives.ReadInt64LittleEndian(isaBuffer.Slice(24, 8));
            ulong info = (ulong)BinaryPrimitives.ReadUInt64LittleEndian(isaBuffer.Slice(32, 8));

            return new Objc2Class()
            {
                Isa = isa,
                SuperClass = superClass,
                Name = name,
                Version = version,
                Info = info
            };
        }

        public string? GetClassName(Objc2Object @object)
        {
            if (@object == Objc2Object.Null || @object.IsSmallObject)
            {
                return null;
            }

            var @class = this.ReadClass(@object.Isa);
            return this.GetClassName(@class);
        }

        public string? GetClassName(Objc2Class objcClass)
        {
            if (objcClass == Objc2Class.Null)
            {
                return null;
            }

            var nameBuffer = new byte[128]; // Assuming class name won't exceed 128 bytes
            this.processMemoryReader.Read(objcClass.Name, nameBuffer);
            var nameLength = nameBuffer.IndexOf((byte)0);
            var className = System.Text.Encoding.UTF8.GetString(nameBuffer, 0, nameLength);
            return className;
        }

        // Small objects have no isa; the class is implied by the tag.
        public static string? GetSmallObjectClassName(SmallObjectType type) => type switch
        {
            SmallObjectType.String => "NSString",
            SmallObjectType.Int or SmallObjectType.Float or SmallObjectType.ExtendedDouble or SmallObjectType.RepeatingDouble => "NSNumber",
            SmallObjectType.Date => "NSDate",
            _ => null,
        };
    }
}
