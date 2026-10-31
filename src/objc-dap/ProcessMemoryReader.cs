using Microsoft;
using System.Buffers.Binary;
using System.ComponentModel;

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

        public void ReadExactly(nuint baseAddress, Span<byte> buffer)
        {
            int bytesRead;

            try
            {
                bytesRead = this.Read(baseAddress, buffer);
            }
            catch (Win32Exception ex)
            {
                // Memory which can't be read is part of debugging, e.g. a pointer in an object that isn't initialized yet.
                throw new InvalidOperationException($"Could not read process memory at 0x{baseAddress:X}.", ex);
            }

            if (bytesRead != buffer.Length)
            {
                throw new InvalidOperationException($"Could not read the expected number of bytes from process memory. Expected {buffer.Length}, but read {bytesRead}.");
            }
        }

        public ulong ReadUInt64LittleEndian(nuint baseAddress)
        {
            Span<byte> dataPtrMemory = stackalloc byte[8];
            this.ReadExactly(baseAddress, dataPtrMemory);
            return BinaryPrimitives.ReadUInt64LittleEndian(dataPtrMemory);
        }

        public bool IsAligned(ulong address) => (address & (nuint)(IntPtr.Size - 1)) == 0;

        public abstract bool IsDereferencableMemory(nuint baseAddress);

        /// <inheritdoc/>
        public abstract void Dispose();
    }
}
