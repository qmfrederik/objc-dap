namespace Objc.Dap
{
    /// <summary>
    /// Maps variable references issued by the proxy to the address of the object they expand.
    /// </summary>
    public class VariableReferenceStore
    {
        // References issued by lldb-dap are small sequential integers; start well above them to avoid collisions.
        public const int FirstReference = 0x40000000;

        private readonly Dictionary<int, nuint> references = new Dictionary<int, nuint>();
        private int nextReference = FirstReference;

        public int Count
        {
            get
            {
                lock (this.references)
                {
                    return this.references.Count;
                }
            }
        }

        public int Add(nuint address)
        {
            lock (this.references)
            {
                int reference = this.nextReference++;
                this.references.Add(reference, address);
                return reference;
            }
        }

        public bool TryGetAddress(int reference, out nuint address)
        {
            lock (this.references)
            {
                return this.references.TryGetValue(reference, out address);
            }
        }

        // The counter is not reset so a stale client reference can never resolve to a newer object.
        public void Clear()
        {
            lock (this.references)
            {
                this.references.Clear();
            }
        }
    }
}
