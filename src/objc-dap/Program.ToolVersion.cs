using Microsoft.Extensions.Logging.Abstractions;
using System.CommandLine;
using System.Diagnostics;
using System.IO.Abstractions;

namespace Objc.Dap
{
    public partial class Program
    {
        private static void DoToolVersionCommand(ParseResult parseResult)
        {
            var lldbDapLocator = new LldbDapLocator(new FileSystem(), NullLogger<LldbDapLocator>.Instance);
            var lldbDapPath = lldbDapLocator.DefaultLldbDapPath();

            var pythonLocator = new PythonLocator(new FileSystem(), NullLogger<PythonLocator>.Instance);
            var python3Library = pythonLocator.TryFindPython3Runtime(new Version(3, 10));

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
    }
}
