namespace Objc.Dap.Runtime
{
    public record Objc2InstanceVariable
    {
        public required string Name { get; init; }

        public required string TypeEncoding { get; init; }

        /// <summary>
        /// Gets the offset, in bytes, of the instance variable from the start of the object.
        /// </summary>
        public int Offset { get; init; }

        public uint Size { get; init; }
    }
}
