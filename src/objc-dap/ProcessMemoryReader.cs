using Microsoft;

namespace Objc.Dap
{
    public abstract class ProcessMemoryReader : IDisposableObservable
    {
        public static ProcessMemoryReader Create()
        {
            if (OperatingSystem.IsOSPlatformVersionAtLeast("Windows", 5, 1, 2600))
            {
                return new Win32ProcessMemoryReader();
            }

            throw new PlatformNotSupportedException("Process memory reading is not supported on this platform.");
        }

        /// <inheritdoc/>
        public virtual bool IsDisposed { get; }

        public abstract void Open(int processId);

        public abstract int Read(nuint baseAddress, Span<byte> buffer);

        public abstract bool IsDereferencableMemory(nuint baseAddress);

        /// <inheritdoc/>
        public abstract void Dispose();
    }
}
