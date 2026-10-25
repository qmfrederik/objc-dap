using Microsoft.Extensions.Logging;
using System.IO.Abstractions;

namespace Objc.Dap
{
    public class PythonLocator
    {
        private readonly IFileSystem fileSystem;
        private readonly ILogger<PythonLocator> logger;

        public PythonLocator(IFileSystem fileSystem, ILogger<PythonLocator> logger)
        {
            this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public IFileInfo? TryFindPython3Runtime(Version minimumVersion)
        {
            string? environmentPath = this.GetPythonLibraryPathVariable();

            if (environmentPath is not null)
            {
                return this.fileSystem.FileInfo.New(environmentPath);
            }

            if (!OperatingSystem.IsWindows())
            {
                return null;
            }

            // Probe pythoncore-3.*-64\python3.dll folders in %LOCALAPPDATA%\Python
            var pythonDirectory =
                this.fileSystem.DirectoryInfo.New(
                    Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Python"));

            if (pythonDirectory.Exists)
            {
                this.logger.LogInformation($"Inspecting Python installations in {pythonDirectory.FullName}");
                foreach (var directory in
                        pythonDirectory
                            .GetDirectories("pythoncore-3.*-64", SearchOption.TopDirectoryOnly)
                            .OrderByDescending(d => d.Name, StringComparer.OrdinalIgnoreCase))
                {
                    this.logger.LogInformation($"Found directory {directory.FullName}.");
                    var version = new Version(directory.Name.Split('-')[1]);

                    if (version < minimumVersion)
                    {
                        this.logger.LogInformation($"Skipping directory {directory.FullName} because its version {version} is less than the minimum required version {minimumVersion}");
                        continue;
                    }

                    var python3File = this.fileSystem.FileInfo.New(Path.Combine(directory.FullName, "python3.dll"));

                    if (python3File.Exists)
                    {
                        this.logger.LogInformation($"Found python3.dll at {python3File.FullName}");
                        return python3File;
                    }
                    else
                    {
                        this.logger.LogInformation($"python3.dll not found in {directory.FullName}");
                    }
                }
            }

            // Use the path environment variable, find Python installations, and then probe for {version}\x64\python3.dll files.
            var path = this.GetPathVariable();

            if (path != null)
            {
                foreach (var directoryName in path.Split(";", StringSplitOptions.RemoveEmptyEntries))
                {
                    var pathDirectory = this.fileSystem.DirectoryInfo.New(directoryName);

                    if (!pathDirectory.Exists)
                    {
                        continue;
                    }

                    this.logger.LogInformation($"Inspecting directory {pathDirectory.FullName} from PATH environment variable.");

                    if (string.Equals(pathDirectory.Parent?.Parent?.Name, "Python", StringComparison.InvariantCultureIgnoreCase))
                    {
                        pythonDirectory = pathDirectory.Parent!.Parent!;

                        this.logger.LogInformation($"Found potential Python directory at {pythonDirectory.FullName}");

                        // Probe all child directories which are a potential Python version number, and search for x64\python3.dll
                        foreach (var directory in
                                pythonDirectory
                                    .GetDirectories("3.*", SearchOption.TopDirectoryOnly)
                                    .OrderByDescending(d => d.Name, StringComparer.OrdinalIgnoreCase))
                        {
                            if (!directory.GetDirectories("x64", SearchOption.TopDirectoryOnly).Any())
                            {
                                this.logger.LogInformation($"Skipping directory {directory.FullName} because it does not contain an x64 subdirectory.");
                                continue;
                            }

                            if (!directory.GetDirectories("x64", SearchOption.TopDirectoryOnly).Single().GetFiles("python3.dll").Any())
                            {
                                this.logger.LogInformation($"Skipping directory {directory.FullName} because it does not contain python3.dll in the x64 subdirectory.");
                                continue;
                            }

                            this.logger.LogInformation($"Found directory {directory.FullName}.");
                            var version = new Version(directory.Name);

                            if (version < minimumVersion)
                            {
                                this.logger.LogInformation($"Skipping directory {directory.FullName} because its version {version} is less than the minimum required version {minimumVersion}");
                                continue;
                            }

                            return directory
                                .GetDirectories("x64", SearchOption.TopDirectoryOnly).Single()
                                .GetFiles("python3.dll").Single();
                        }
                    }
                }
            }

            return null;
        }

        public virtual string? GetPathVariable() => Environment.GetEnvironmentVariable("PATH");
        public virtual string? GetPythonLibraryPathVariable() => Environment.GetEnvironmentVariable("PYTHON3_LIBRARY_PATH");
    }
}
