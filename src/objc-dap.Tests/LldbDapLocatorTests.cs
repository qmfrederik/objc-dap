using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Objc.Dap;
using Testably.Abstractions.Testing;

namespace ObjcDap.Tests
{
    public class LldbDapLocatorTests
    {
        [Fact]
        public void Constructor_ValidatesArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new LldbDapLocator(null!, NullLogger<LldbDapLocator>.Instance));
            Assert.Throws<ArgumentNullException>(() => new LldbDapLocator(new MockFileSystem(), null!));
        }

        [WindowsFact]
        public void DefaultLldbDapPath_ProbesProgramFiles()
        {
            var fileSystem = new MockFileSystem();
            var locator = new Mock<LldbDapLocator>(fileSystem, NullLogger<LldbDapLocator>.Instance)
            {
                CallBase = true,
            };

            locator.Setup(l => l.GetLldbDapPathVariable()).Returns((string?)null);

            var programFilesPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "LLVM",
                "bin",
                "lldb-dap.exe");

            fileSystem.Initialize().WithFile(programFilesPath);

            Assert.Equal(
                programFilesPath,
                locator.Object.DefaultLldbDapPath().FullName);
        }

        [WindowsFact]
        public void DefaultLldbDapPath_FallsBackToFilename()
        {
            var fileSystem = new MockFileSystem();
            var locator = new Mock<LldbDapLocator>(fileSystem, NullLogger<LldbDapLocator>.Instance)
            {
                CallBase = true,
            };

            locator.Setup(l => l.GetLldbDapPathVariable()).Returns((string?)null);

            Assert.Equal(
                "lldb-dap.exe",
                locator.Object.DefaultLldbDapPath().Name);
        }
    }
}
