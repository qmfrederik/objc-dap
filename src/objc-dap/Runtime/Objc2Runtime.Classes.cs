using System.Buffers;

namespace Objc.Dap.Runtime
{
    public partial class Objc2Runtime
    {
        private const int MaxClassChainDepth = 64;
        private const int MaxClassNameLength = 128;

        private static readonly SearchValues<byte> IdentifierBytes =
            SearchValues.Create("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_$."u8);

        private readonly HashSet<nuint> validatedClasses = new HashSet<nuint>();

        /// <summary>
        /// Uses heuristics to determine whether <paramref name="address"/> is an objc_class whose superclass chain is also made of valid classes.
        /// </summary>
        private bool ValidateClassHierarchy(nuint address)
        {
            // A NUL pointer isn't a valid class hierarchy.
            if (address == 0)
            {
                return false;
            }

            var chain = new List<nuint>();
            nuint current = address;

            while (!this.validatedClasses.Contains(current)
                // The root class has a superclass of 0, so we can stop when we reach that.
                && current != 0)
            {
                if (chain.Count >= MaxClassChainDepth || !this.TryValidateClassStruct(current, out nuint superClass))
                {
                    return false;
                }

                chain.Add(current);
                current = superClass;
            }

            this.validatedClasses.UnionWith(chain);
            return true;
        }

        /// <summary>
        /// Validates an individual class, and checks whether it meets the basic requirements of a valid objc_class.
        /// </summary>
        private bool TryValidateClassStruct(nuint address, out nuint superClass)
        {
            superClass = 0;

            // Memory needs to be aligned and dereferencable.
            if (!this.processMemoryReader.IsAligned(address) || !this.processMemoryReader.IsDereferencableMemory(address))
            {
                return false;
            }

            try
            {
                var @class = this.ReadClass(address);

                // ISA poiner needs to be aligned and dereferencable, and the name pointer needs to be dereferencable.
                if (@class == Objc2Class.Null
                    || !this.processMemoryReader.IsAligned(@class.Isa)
                    || !this.processMemoryReader.IsDereferencableMemory(@class.Isa)
                    || !this.processMemoryReader.IsDereferencableMemory(@class.Name))
                {
                    return false;
                }

                // The name must be a valid identifier.
                Span<byte> name = stackalloc byte[MaxClassNameLength];
                int read = this.processMemoryReader.Read(@class.Name, name);
                if (!IsIdentifier(name.Slice(0, read)))
                {
                    return false;
                }

                superClass = @class.SuperClass;
                return true;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // The name or metaclass crossed into unreadable memory.
                return false;
            }
        }

        private static bool IsIdentifier(ReadOnlySpan<byte> bytes)
        {
            int length = bytes.IndexOf((byte)0);
            return length > 0
                && !char.IsAsciiDigit((char)bytes[0])
                && !bytes.Slice(0, length).ContainsAnyExcept(IdentifierBytes);
        }
    }
}