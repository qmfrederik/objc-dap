using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;

namespace Objc.Dap
{
    public class Program
    {
        /// <summary>
        /// Gets the path to the <c>lldb-dap</c> executable.  If the environment variable <c>LLDB_DAP_PATH</c> is set, its value is used.
        /// On Windows, probe for LLVM installation in <c>C:\Program Files\LLVM\bin\lldb-dap.exe</c>.
        /// If not found, default to <c>lldb-dap[.exe]</c> and rely on the PATH environment variable to locate it.
        /// </summary>
        public static FileInfo DefaultLddbDapPath(ArgumentResult? argumentResult = null)
        {
            string? environmentPath = Environment.GetEnvironmentVariable("LLDB_DAP_PATH");

            if (environmentPath is not null)
            {
                return new FileInfo(environmentPath);
            }

            if (OperatingSystem.IsWindows())
            {
                var programFilesPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "LLVM",
                    "bin",
                    "lldb-dap.exe");

                if (File.Exists(programFilesPath))
                {
                    return new FileInfo(programFilesPath);
                }

                return new FileInfo("lldb-dap.exe");
            }
            else
            {
                return new FileInfo("lldb-dap");
            }
        }

        /// <summary>
        /// Attempts to locate the Python 3 library file on Windows.
        /// </summary>
        /// <remarks>Returns <see langword="null"/> on non-Windows platforms.</remarks>A <see cref="FileInfo"/> for the located Python 3 library; otherwise, <see langword="null"/>.
        public static FileInfo? TryFindPyton3Library()
        {
            if (!OperatingSystem.IsWindows())
            {
                return null;
            }

            var pythonDirectory =
                new DirectoryInfo(
                    Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Python"));

            if (!pythonDirectory.Exists)
            {
                return null;
            }

            foreach (var directory in
                    pythonDirectory
                        .GetDirectories("pythoncore-3.*-64", SearchOption.TopDirectoryOnly)
                        .OrderByDescending(d => d.Name, StringComparer.OrdinalIgnoreCase))
            {
                var python3File = new FileInfo(Path.Combine(directory.FullName, "python3.dll"));

                if (python3File.Exists)
                {
                    return python3File;
                }
            }

            return null;
        }

        private static void DoToolVersionCommand(ParseResult parseResult)
        {
            var lldbDapPath = DefaultLddbDapPath();
            var python3Library = TryFindPyton3Library();
            Version? lldbVersion = null;
            Version? pythonVersion = null;

            if (OperatingSystem.IsWindows() && lldbDapPath != null && lldbDapPath.Exists)
            {
                FileVersionInfo lldbVersionInfo = FileVersionInfo.GetVersionInfo(Path.GetFullPath(lldbDapPath.FullName));
                lldbVersion = new Version(lldbVersionInfo.FileMajorPart, lldbVersionInfo.FileMinorPart, lldbVersionInfo.FileBuildPart);
            }

            if (OperatingSystem.IsWindows() && python3Library != null && python3Library.Exists)
            {
                FileVersionInfo pythonVersionInfo = FileVersionInfo.GetVersionInfo(Path.GetFullPath(python3Library.FullName));
                pythonVersion = new Version(pythonVersionInfo.FileMajorPart, pythonVersionInfo.FileMinorPart, pythonVersionInfo.FileBuildPart, pythonVersionInfo.FilePrivatePart);
            }

            Console.WriteLine($"lldb-dap.exe: {lldbDapPath?.FullName} ({lldbVersion})");
            Console.WriteLine($"python3.dll:  {python3Library?.FullName} ({pythonVersion})");
        }

        private static Command ToolVersionCommand()
        {
            var command = new Command("tool-version", "List the LLDB, objc-dap version and the Python version used by objc-dap.");
            command.SetAction(DoToolVersionCommand);
            return command;
        }

        public static int Main(string[] args)
        {
            RootCommand rootCommand = new("Debug Adapter for Objective-C applications");
            rootCommand.Add(ToolVersionCommand());

            var lldbDapPathOption = new Option<FileInfo>("lldb-dap-path")
            {
                Description = "Path to the lldb-dap executable",
                Arity = ArgumentArity.ExactlyOne,
                DefaultValueFactory = DefaultLddbDapPath,
            }.AcceptExistingOnly();

            rootCommand.Options.Add(lldbDapPathOption);
            rootCommand.SetAction(RunAsync);

            return rootCommand.Parse(args).Invoke();
        }

        public static Task<int> RunAsync(ParseResult parseResult, CancellationToken cancellationToken)
        {
            // Has an arity of exactly one, and a "accept existing only" validator, so this will never be null.
            var lldbPath = parseResult.GetValue<FileInfo>("lldb-dap-path")!;

            return RunAsync(
                new DebugAdapterOptions()
                {
                    LldbDapPath = lldbPath,
                    Input = Console.OpenStandardInput(),
                    Output = Console.OpenStandardOutput(),
                    Error = Console.OpenStandardError(),
                    Python3Library = TryFindPyton3Library(),
                },
                cancellationToken);
        }

        public static async Task<int> RunAsync(DebugAdapterOptions options, CancellationToken cancellationToken)
        {
            ServiceCollection services = new ServiceCollection();
            services.AddLogging(l => l.AddDebug());
            services.AddSingleton(options);
            services.AddSingleton<DebugAdapterProxy>();

            using var provider = services.BuildServiceProvider();
            var proxy = provider.GetRequiredService<DebugAdapterProxy>();
            await proxy.RunAsync(cancellationToken);
            return 0;
        }
    }
}