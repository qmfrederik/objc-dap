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

        /// <summary>
        /// Gets a value indicating whether the object is small object.
        /// </summary>
        public bool IsSmallObject => (this.Address & Objc2Runtime.OBJC_SMALL_OBJECT_MASK) != 0;

        /// <summary>
        /// Gets the type of small object.  If the object is not a small object, returns <see cref="SmallObjectType.None"/>.
        /// </summary>
        public SmallObjectType SmallObjectType
        {
            get
            {
                if ((this.Address & Objc2Runtime.OBJC_SMALL_OBJECT_MASK) == 0)
                {
                    return SmallObjectType.None;
                }

                return (SmallObjectType)(this.Address & Objc2Runtime.OBJC_SMALL_OBJECT_MASK);
            }
        }

        public static Objc2Object Null => new Objc2Object();
    }
}
