namespace Objc.Dap.Runtime
{
    /// <summary>
    /// Captures what the runtime knows about an Objective-C object, as returned by <see cref="Objc2Runtime.DescribeObject(Objc2Object)"/>.
    /// </summary>
    public sealed class Objc2ObjectInfo
    {
        /// <summary>
        /// Gets the name of the class of the object, or <see langword="null"/> if it can't be determined.
        /// </summary>
        public string? ClassName { get; init; }

        /// <summary>
        /// Gets the value of the object, e.g. the text of a string or the value of a number,
        /// or <see langword="null"/> if the object has no value of its own.
        /// </summary>
        public object? Value { get; init; }

        /// <summary>
        /// Gets the number of items in a collection, or -1 if the object is not a supported collection.
        /// </summary>
        public int ItemCount { get; init; } = -1;

        /// <summary>
        /// Gets the number of indexed children, or -1 if the object has none.
        /// </summary>
        public int IndexedVariablesCount { get; init; } = -1;

        /// <summary>
        /// Gets the number of named children, or -1 if the object has none. For objects which are not collections,
        /// these are their instance variables.
        /// </summary>
        public int NamedVariablesCount { get; init; } = -1;

        /// <summary>
        /// Gets the address of the buffer of an <c>NSData</c>, or <see langword="null"/> if the object has no buffer.
        /// </summary>
        public nuint? DataBuffer { get; init; }
    }
}
