using Microsoft.Extensions.Logging;
using System.IO.Abstractions;

namespace Objc.Dap
{
    public class LldbDapLocator
    {
        private readonly IFileSystem fileSystem;
        private readonly ILogger<LldbDapLocator> logger;

        public LldbDapLocator(IFileSystem fileSystem, ILogger<LldbDapLocator> logger)
        {
            this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets the path to the <c>lldb-dap</c> executable.  If the environment variable <c>LLDB_DAP_PATH</c> is set, its value is used.
        /// On Windows, probe for LLVM installation in <c>C:\Program Files\LLVM\bin\lldb-dap.exe</c>.
        /// If not found, default to <c>lldb-dap[.exe]</c> and rely on the PATH environment variable to locate it.
        /// </summary>
        public IFileInfo DefaultLldbDapPath()
        {
            string? environmentPath = this.GetLldbDapPathVariable();

            if (environmentPath is not null)
            {
                return this.fileSystem.FileInfo.New(environmentPath);
            }

            if (OperatingSystem.IsWindows())
            {
                var programFilesPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "LLVM",
                    "bin",
                    "lldb-dap.exe");

                if (this.fileSystem.File.Exists(programFilesPath))
                {
                    return this.fileSystem.FileInfo.New(programFilesPath);
                }

                return this.fileSystem.FileInfo.New("lldb-dap.exe");
            }
            else
            {
                return this.fileSystem.FileInfo.New("lldb-dap");
            }
        }

        public virtual string? GetLldbDapPathVariable() => Environment.GetEnvironmentVariable("LLDB_DAP_PATH");
    }
}
