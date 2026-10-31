namespace Objc.Dap.Runtime
{
    public partial class Objc2Runtime
    {
        /// <summary>
        /// Gathers what the runtime knows about <paramref name="object"/>: its class, its value, and its children.
        /// </summary>
        public Objc2ObjectInfo DescribeObject(Objc2Object @object)
        {
            ArgumentNullException.ThrowIfNull(@object);

            var className = this.GetClassName(@object) ?? GetSmallObjectClassName(@object.SmallObjectType);
            var value = this.GetObjectValue(@object);

            var indexedCount = this.GetIndexedVariablesCount(@object);
            var namedCount = this.GetNamedVariablesCount(@object);
            var itemCount = Math.Max(indexedCount, namedCount);

            // Objects which are not collections expose their instance variables.
            if (indexedCount < 0 && namedCount < 0)
            {
                namedCount = this.TryGetInstanceVariables(@object)?.Count ?? -1;
            }

            return new Objc2ObjectInfo()
            {
                ClassName = className,
                Value = value,
                ItemCount = itemCount,
                IndexedVariablesCount = indexedCount,
                NamedVariablesCount = namedCount,
                DataBuffer = this.TryGetDataBuffer(@object, out var dataBuffer, out _) ? dataBuffer : null,
            };
        }

        /// <summary>
        /// Gets the instance variables of <paramref name="object"/>.
        /// </summary>
        /// <returns>
        /// <see langword="null"/> if the object has no instance variables or its class metadata can't be read.
        /// </returns>
        public IReadOnlyList<Objc2InstanceVariable>? TryGetInstanceVariables(Objc2Object @object)
        {
            try
            {
                var instanceVariables = this.GetInstanceVariables(@object);
                return instanceVariables.Count > 0 ? instanceVariables : null;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }
    }
}
