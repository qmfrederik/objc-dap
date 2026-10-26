namespace Objc.Dap.Runtime
{
    // Defined at https://github.com/gnustep/libobjc2/blob/master/class.h
    public record Objc2Class
    {
        public nuint Isa { get; init; }
        public nuint SuperClass { get; init; }
        public nuint Name { get; init; }
        public long Version { get; init; }
        public ulong Info { get; init; }
        
        public static Objc2Class Null => new Objc2Class();
    }
}
