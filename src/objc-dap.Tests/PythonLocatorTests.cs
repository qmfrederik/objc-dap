using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Objc.Dap;
using Testably.Abstractions.Testing;

namespace ObjcDap.Tests
{
    public class PythonLocatorTests
    {
        [Fact]
        public void Constructor_ValidatesArguments()
        {
            var fileSystem = new MockFileSystem();
            var logger = new NullLogger<PythonLocator>();
            Assert.Throws<ArgumentNullException>(() => new PythonLocator(null!, logger));
            Assert.Throws<ArgumentNullException>(() => new PythonLocator(fileSystem, null!));
        }

        [Fact]
        public void TryFindPython3Runtime_RespectsEnvironmentVariable()
        {
            var locator = new Mock<PythonLocator>(new MockFileSystem(), NullLogger<PythonLocator>.Instance)
            {
                CallBase = true
            };
            locator.Setup(l => l.GetPythonLibraryPathVariable()).Returns("C:\\Python39\\python39.dll");

            Assert.Equal(
                new FileInfo("C:\\Python39\\python39.dll").FullName,
                locator.Object.TryFindPython3Runtime(new Version(3, 9))?.FullName);
        }

        [WindowsFact]
        public void TryFindPython3Runtime_SearchesInAppData()
        {
            var fileSystem = new MockFileSystem();
            var locator = new Mock<PythonLocator>(fileSystem, NullLogger<PythonLocator>.Instance)
            {
                CallBase = true,
            };

            locator.Setup(l => l.GetPythonLibraryPathVariable()).Returns((string?)null);

            var pythonDirectory =
                Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Python");

            fileSystem
                .Initialize()
                .WithFile(Path.Combine(pythonDirectory, "pythoncore-3.15-64", "python3.dll"))
                .WithFile(Path.Combine(pythonDirectory, "pythoncore-3.14-64", "python3.dll"))
                .WithFile(Path.Combine(pythonDirectory, "pythoncore-3.16-32", "python3.dll"));

            Assert.Equal(
                Path.Combine(pythonDirectory, "pythoncore-3.15-64", "python3.dll"),
                locator.Object.TryFindPython3Runtime(new Version(3, 9))?.FullName);
        }

        [WindowsFact]
        public void TryFindPython3Runtime_SearchesInPath()
        {
            var fileSystem = new MockFileSystem();
            var locator = new Mock<PythonLocator>(fileSystem, NullLogger<PythonLocator>.Instance)
            {
                CallBase = true,
            };

            locator.Setup(l => l.GetPythonLibraryPathVariable()).Returns((string?)null);
            locator.Setup(l => l.GetPathVariable()).Returns(
                @"C:\Windows\system32;" +
                @"C:\Windows;" +
                @"C:\hostedtoolcache\windows\Python\3.12.10\x64;" +
                @"C:\hostedtoolcache\windows\Python\3.12.10\x64\Scripts;");

            fileSystem
                .Initialize()
                .WithFile(@"C:\hostedtoolcache\windows\Python\3.14.8\x64\python3.dll")
                .WithFile(@"C:\hostedtoolcache\windows\Python\3.13.16\x64\python3.dll")
                .WithFile(@"C:\hostedtoolcache\windows\Python\3.13.16\x86\python3.dll")
                .WithFile(@"C:\hostedtoolcache\windows\Python\3.12.10\x64\python3.dll")
                .WithFile(@"C:\hostedtoolcache\windows\Python\3.12.10\x86\python3.dll")
                .WithFile(@"C:\hostedtoolcache\windows\Python\3.11.9\x64\python3.dll")
                .WithFile(@"C:\hostedtoolcache\windows\Python\3.11.9\x86\python3.dll")
                .WithFile(@"C:\hostedtoolcache\windows\Python\3.10.11\x64\python3.dll");

            Assert.Equal(
                @"C:\hostedtoolcache\windows\Python\3.14.8\x64\python3.dll",
                locator.Object.TryFindPython3Runtime(new Version(3, 9))?.FullName);
        }
    }
}
