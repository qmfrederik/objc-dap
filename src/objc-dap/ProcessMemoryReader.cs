using Microsoft;

namespace Objc.Dap
{
    public abstract class ProcessMemoryReader : IDisposableObservable
    {
        /// <inheritdoc/>
        public virtual bool IsDisposed { get; }

        public abstract void Open(int processId);

        public abstract int Read(nuint baseAddress, Span<byte> buffer);

        public abstract bool IsDereferencableMemory(nuint baseAddress);

        /// <inheritdoc/>
        public abstract void Dispose();
    }
}
