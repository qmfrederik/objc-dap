using Microsoft;
using Microsoft.Win32.SafeHandles;
using System.Globalization;
using System.Runtime.Versioning;

namespace Objc.Dap
{
    /// <summary>
    /// Reads the memory of another process through <c>/proc/{pid}/mem</c>.
    /// </summary>
    /// <remarks>
    /// Access is governed by ptrace permissions, so reading a process which is not a
    /// descendant of the current process may fail unless <c>CAP_SYS_PTRACE</c> is available.
    /// </remarks>
    [SupportedOSPlatform("linux")]
    public class LinuxProcessMemoryReader : ProcessMemoryReader
    {
        private SafeFileHandle? handle;
        private int processId;
        private bool disposed;

        public override bool IsDisposed => this.disposed;

        public override void Open(int processId)
        {
            Verify.NotDisposed(this);
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(processId, 0, nameof(processId));

            if (this.handle != null)
            {
                throw new InvalidOperationException("Process is already open.");
            }

            try
            {
                this.handle = File.OpenHandle(
                    $"/proc/{processId}/mem",
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite);
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                throw new InvalidOperationException($"Process {processId} does not exist. It may have already exited.", ex);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                throw new InvalidOperationException(
                    $"Failed to open process memory for process {processId}. {ex.Message}",
                    ex);
            }

            this.processId = processId;
        }

        public override int Read(nuint baseAddress, Span<byte> buffer)
        {
            Verify.NotDisposed(this);
            SafeFileHandle handle = this.EnsureProcessOpen();
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(baseAddress, (nuint)0, nameof(baseAddress));

            int totalRead = 0;
            while (totalRead < buffer.Length)
            {
                int bytesRead;
                try
                {
                    bytesRead = RandomAccess.Read(handle, buffer.Slice(totalRead), (long)baseAddress + totalRead);
                }
                catch (IOException) when (totalRead > 0)
                {
                    // Hit an unmapped page after a partial read; report what we got.
                    break;
                }

                if (bytesRead == 0)
                {
                    break;
                }

                totalRead += bytesRead;
            }

            return totalRead;
        }

        public override bool IsDereferencableMemory(nuint baseAddress)
        {
            Verify.NotDisposed(this);
            this.EnsureProcessOpen();

            if (baseAddress == 0)
            {
                return false;
            }

            // /proc/pid/mem reads with FOLL_FORCE and so can read pages which are not readable
            // by the process itself. Consult the mappings for the actual permissions instead.
            try
            {
                return ContainsReadableAddress(File.ReadLines($"/proc/{this.processId}/maps"), baseAddress);
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                // The process has exited.
                return false;
            }
        }

        private static bool ContainsReadableAddress(IEnumerable<string> mapsLines, nuint address)
        {
            foreach (string line in mapsLines)
            {
                // Format: start-end perms offset dev inode [path]
                ReadOnlySpan<char> span = line;
                int dash = span.IndexOf('-');
                int firstSpace = span.IndexOf(' ');
                if (dash < 0 || firstSpace < dash || firstSpace + 1 >= span.Length)
                {
                    continue;
                }

                if (!ulong.TryParse(span[..dash], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ulong start)
                    || !ulong.TryParse(span[(dash + 1)..firstSpace], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ulong end))
                {
                    continue;
                }

                if (address < start || address >= end)
                {
                    continue;
                }

                // [vvar] mappings cannot be read through /proc/pid/mem even when marked readable.
                return span[firstSpace + 1] == 'r' && !span.Contains("[vvar", StringComparison.Ordinal);
            }

            return false;
        }

        private SafeFileHandle EnsureProcessOpen()
        {
            return this.handle ?? throw new InvalidOperationException("Process is not open.");
        }

        public override void Dispose()
        {
            this.disposed = true;
            this.handle?.Dispose();
        }
    }
}