using Objc.Dap;
using System.Runtime.Versioning;

namespace ObjcDap.Tests
{
    /// <summary>
    /// Tests the <see cref="LinuxProcessMemoryReader"/> class.
    /// </summary>
    [SupportedOSPlatform("linux")]
    public class LinuxProcessMemoryReaderTests
    {
        [LinuxFact]
        public void Open_ValidatesArguments()
        {
            using var reader = new LinuxProcessMemoryReader();
            Assert.Throws<ArgumentOutOfRangeException>(() => reader.Open(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => reader.Open(-1));

            reader.Open(Environment.ProcessId);
            Assert.Throws<InvalidOperationException>(() => reader.Open(Environment.ProcessId));
        }

        [LinuxFact]
        public void NotOpen_ValidatesArguments()
        {
            using var reader = new LinuxProcessMemoryReader();
            Assert.Throws<InvalidOperationException>(() => reader.Read(1, Span<byte>.Empty));
            Assert.Throws<InvalidOperationException>(() => reader.IsDereferencableMemory(1));
        }

        [LinuxFact]
        public void Read_ValidatesArguments()
        {
            using var reader = new LinuxProcessMemoryReader();
            reader.Open(Environment.ProcessId);

            Memory<byte> buffer = new byte[4];

            Assert.Throws<ArgumentOutOfRangeException>(() => reader.Read(0, buffer.Span));
            Assert.ThrowsAny<IOException>(() => reader.Read(1, buffer.Span));
        }

        [LinuxFact]
        public unsafe void Read_Works()
        {
            using var reader = new LinuxProcessMemoryReader();
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

        [LinuxFact]
        public unsafe void IsDereferencableMemory_Works()
        {
            using var reader = new LinuxProcessMemoryReader();
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
