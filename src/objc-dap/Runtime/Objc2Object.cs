namespace Objc.Dap.Runtime
{
    // Defined at https://github.com/gnustep/libobjc2/blob/master/objc/runtime.h#L90
    public record Objc2Object
    {
        public nuint Isa { get; init; }

        public static Objc2Object Null => new Objc2Object();
    }
}
