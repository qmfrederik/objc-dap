using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NReco.Logging.File;
using Objc.Dap.Runtime;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.IO.Abstractions;

namespace Objc.Dap
{
    public partial class Program
    {
        /// <summary>
        /// Gets the path to the <c>lldb-dap</c> executable.  If the environment variable <c>LLDB_DAP_PATH</c> is set, its value is used.
        /// On Windows, probe for LLVM installation in <c>C:\Program Files\LLVM\bin\lldb-dap.exe</c>.
        /// If not found, default to <c>lldb-dap[.exe]</c> and rely on the PATH environment variable to locate it.
        /// </summary>
        public static FileInfo DefaultLddbDapPath(ArgumentResult? argumentResult = null)
        {
            var locator = new LldbDapLocator(new FileSystem(), NullLogger<LldbDapLocator>.Instance);
            return new FileInfo(locator.DefaultLldbDapPath().FullName);
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

        public static int Main(string[] args)
        {
            RootCommand rootCommand = new("Debug Adapter for Objective-C applications");

            var lldbDapPathOption = new Option<FileInfo>("lldb-dap-path")
            {
                Description = "Path to the lldb-dap executable",
                Arity = ArgumentArity.ExactlyOne,
                DefaultValueFactory = DefaultLddbDapPath,
            }.AcceptExistingOnly();

            var breakOption = new Option<bool>("--break")
            {
                Description = "Break into the C# debugger on startup, allowing you to debug the objc-dap itself.",
                Arity = ArgumentArity.ZeroOrOne,
            };

            var logFileOption = new Option<FileInfo?>("--log-file")
            {
                Description = "Path of a file to which log messages are appended. If not set, no log file is written.",
                Arity = ArgumentArity.ExactlyOne,
            };

            var logLevelOption = new Option<LogLevel>("--log-level")
            {
                Description = "Minimum level of the messages written to the log file.",
                Arity = ArgumentArity.ExactlyOne,
                DefaultValueFactory = _ => LogLevel.Information,
            };

            rootCommand.Options.Add(lldbDapPathOption);
            rootCommand.Options.Add(breakOption);
            rootCommand.Options.Add(logFileOption);
            rootCommand.Options.Add(logLevelOption);
            rootCommand.SetAction(RunAsync);

            rootCommand.Add(ToolVersionCommand());

            return rootCommand.Parse(args).Invoke();
        }

        public static Task<int> RunAsync(ParseResult parseResult, CancellationToken cancellationToken)
        {
            // Has an arity of exactly one, and a "accept existing only" validator, so this will never be null.
            var lldbPath = parseResult.GetValue<FileInfo>("lldb-dap-path")!;
            var @break = parseResult.GetValue<bool>("--break");

            if (@break)
            {
                Debugger.Launch();
            }

            return RunAsync(
                new DebugAdapterOptions()
                {
                    LldbDapPath = lldbPath,
                    Input = Console.OpenStandardInput(),
                    Output = Console.OpenStandardOutput(),
                    Error = Console.OpenStandardError(),
                    Python3Library = TryFindPyton3Library(),
                    LogFile = parseResult.GetValue<FileInfo?>("--log-file"),
                    LogLevel = parseResult.GetValue<LogLevel>("--log-level"),
                },
                cancellationToken);
        }

        public static async Task<int> RunAsync(DebugAdapterOptions options, CancellationToken cancellationToken)
        {
            ServiceCollection services = new ServiceCollection();
            services.AddLogging(l =>
            {
                l.SetMinimumLevel(options.LogLevel);
                l.AddDebug();

                if (options.LogFile is not null)
                {
                    l.AddFile(options.LogFile.FullName, fileOptions =>
                    {
                        fileOptions.Append = false;
                        fileOptions.MinLevel = options.LogLevel;
                    });
                }
            });
            services.AddSingleton(options);
            services.AddSingleton(ProcessMemoryReader.Create());
            services.AddSingleton<Objc2Runtime>();
            services.AddSingleton<VariableReferenceStore>();
            services.AddSingleton<DebugAdapterProxy, Objc2DebugAdapterProxy>();

            using var provider = services.BuildServiceProvider();
            var proxy = provider.GetRequiredService<DebugAdapterProxy>();
            var logger = provider.GetRequiredService<ILogger<Program>>();

            try
            {
                await proxy.RunAsync(cancellationToken);
                return 0;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"An error occurred while running the debug adapter: {ex.Message}.");
                Console.Error.WriteLine(ex);
                return 1;
            }
        }
    }
}