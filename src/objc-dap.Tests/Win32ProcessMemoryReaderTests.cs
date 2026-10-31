using Objc.Dap;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;

namespace ObjcDap.Tests
{
    /// <summary>
    /// Tests the <see cref="Win32ProcessMemoryReader"/> class.
    /// </summary>
    [SupportedOSPlatform("windows5.1.2600")]
    public class Win32ProcessMemoryReaderTests
    {
        [WindowsFact]
        public void Open_ValidatesArguments()
        {
            using var reader = new Win32ProcessMemoryReader();
            reader.Open(Process.GetCurrentProcess().Id);
            Assert.Throws<ArgumentOutOfRangeException>(() => reader.Open(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => reader.Open(-1));
        }

        [WindowsFact]
        public unsafe void Open_CanReplaceTheProcess()
        {
            // A restarted debuggee, or another one, is reported with a new process event.
            using var reader = new Win32ProcessMemoryReader();
            reader.Open(Environment.ProcessId);
            reader.Open(Environment.ProcessId);

            Span<byte> source = stackalloc byte[4] { 1, 2, 3, 4 };
            Span<byte> target = stackalloc byte[4];

            fixed (byte* sourcePtr = source)
            {
                Assert.Equal(4, reader.Read((nuint)sourcePtr, target));
                Assert.Equal(source, target);
            }
        }

        [WindowsFact]
        public void ReadExactly_UnreadableMemory_ThrowsInvalidOperationException()
        {
            using var reader = new Win32ProcessMemoryReader();
            reader.Open(Environment.ProcessId);

            Memory<byte> buffer = new byte[4];
            var exception = Assert.Throws<InvalidOperationException>(() => reader.ReadExactly(1, buffer.Span));
            Assert.IsType<Win32Exception>(exception.InnerException);
        }

        [WindowsFact]
        public void NotOpen_ValidatesArguments()
        {
            using var reader = new Win32ProcessMemoryReader();
            Assert.Throws<InvalidOperationException>(() => reader.Read(1, Span<byte>.Empty));
            Assert.Throws<InvalidOperationException>(() => reader.IsDereferencableMemory(1));
        }

        [WindowsFact]
        public void Read_ValidatesArguments()
        {
            using var reader = new Win32ProcessMemoryReader();
            reader.Open(Process.GetCurrentProcess().Id);

            Memory<byte> buffer = new byte[4];

            Assert.Throws<ArgumentOutOfRangeException>(() => reader.Read(0, buffer.Span));
            Assert.Throws<Win32Exception>(() => reader.Read(1, buffer.Span));
        }

        [WindowsFact]
        public unsafe void Read_Works()
        {
            using var reader = new Win32ProcessMemoryReader();
            reader.Open(Environment.ProcessId);
            Span<byte> source = stackalloc byte[4] { 1, 2, 3, 4 };
            Span<byte> target = stackalloc byte[4] { 0, 0, 0, 0 };

            fixed (byte* sourcePtr = source)
            {
                int bytesRead = reader.Read((nuint)sourcePtr, target);
                Assert.Equal(4, bytesRead);
                Assert.Equal(source, target);
            }
        }

        [WindowsFact]
        public unsafe void IsDereferencableMemory_Works()
        {
            using var reader = new Win32ProcessMemoryReader();
            reader.Open(Environment.ProcessId);

            Span<byte> source = stackalloc byte[4] { 1, 2, 3, 4 };

            fixed (byte* sourcePtr = source)
            {
                Assert.True(reader.IsDereferencableMemory((nuint)sourcePtr));
            }

            unchecked
            {
                Assert.False(reader.IsDereferencableMemory((nuint)0xbaadf00dbaadf00d));
            }

            Assert.False(reader.IsDereferencableMemory((nuint)0));
        }
    }
}
