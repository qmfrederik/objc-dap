using Microsoft;
using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Windows.Win32.System.Memory;
using Windows.Win32.System.Threading;
using static Windows.Win32.PInvoke;

namespace Objc.Dap
{
    [SupportedOSPlatform("windows5.1.2600")]
    public class Win32ProcessMemoryReader : ProcessMemoryReader
    {
        private SafeFileHandle? handle;

        public override bool IsDisposed => this.handle != null && this.handle.IsClosed;

        public override void Open(int processId)
        {
            Verify.NotDisposed(this);
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(processId, 0, nameof(processId));

            if (this.handle != null)
            {
                throw new InvalidOperationException("Process is already open.");
            }

            this.handle = OpenProcess_SafeHandle(
                PROCESS_ACCESS_RIGHTS.PROCESS_VM_READ | PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_INFORMATION,
                bInheritHandle: false,
                (uint)processId);

            if (this.handle.IsInvalid)
            {
                throw new Exception($"Failed to open process memory for process {processId}");
            }
        }

        public override unsafe int Read(nuint baseAddress, Span<byte> buffer)
        {
            Verify.NotDisposed(this);
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(baseAddress, (nuint)0, nameof(baseAddress));

            if (this.handle == null)
            {
                throw new InvalidOperationException("Process is not open.");
            }
            ;

            if (!ReadProcessMemory(this.handle, (void*)baseAddress, buffer, out nuint bytesRead))
            {
                var error = Marshal.GetLastWin32Error();
                throw new Win32Exception(error);
            }

            return (int)bytesRead;
        }

        public override unsafe bool IsDereferencableMemory(nuint baseAddress)
        {
            if (VirtualQueryEx(this.handle, (void*)baseAddress, out var memoryBasicInformation) == 0)
            {
                var error = Marshal.GetLastWin32Error();

                if (error == (int)Windows.Win32.Foundation.WIN32_ERROR.ERROR_INVALID_PARAMETER)
                {
                    return false;
                }

                throw new Win32Exception(error);
            }

            return (memoryBasicInformation.State == VIRTUAL_ALLOCATION_TYPE.MEM_COMMIT)
                && !memoryBasicInformation.Protect.HasFlag(PAGE_PROTECTION_FLAGS.PAGE_NOACCESS)
                && !memoryBasicInformation.Protect.HasFlag(PAGE_PROTECTION_FLAGS.PAGE_GUARD);
        }

        /// <inheritdoc/>
        public override void Dispose()
        {
            this.handle?.Dispose();
        }
    }
}
