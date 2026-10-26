namespace Objc.Dap.Runtime
{
    // Defined at https://github.com/gnustep/libobjc2/blob/master/objc/runtime.h#L90
    public record Objc2Object
    {
        /// <summary>
        /// Gets the memory address of the object.
        /// </summary>
        public nuint Address { get; set; }

        /// <summary>
        /// Gets or sets the address of the ISA (class) of the object.
        /// </summary>
        public nuint Isa { get; init; }

        public bool IsTinyString => (this.Address & Objc2Runtime.TINY_STRING_MASK) == Objc2Runtime.TINY_STRING_MASK;

        public static Objc2Object Null => new Objc2Object();
    }
}
