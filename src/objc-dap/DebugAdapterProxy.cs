using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Objc.Dap
{
    /// <summary>
    /// Proxies Debug Adapter Protocol traffic between configured streams and an `lldb-dap` process.
    /// </summary>
    public class DebugAdapterProxy
    {
        private readonly DebugAdapterOptions options;
        private readonly ILogger<DebugAdapterProxy> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DebugAdapterProxy"/> class.
        /// </summary>
        /// <param name="options">The options to configure the debug adapter proxy.</param>
        /// <param name="logger">The logger to use for logging debug information.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="options"/> or <paramref name="logger"/> is null.</exception>
        public DebugAdapterProxy(DebugAdapterOptions options, ILogger<DebugAdapterProxy> logger)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Starts the LLDB DAP process, forwards input/output/error streams between the host and the child process, and
        /// then terminates the process tree when any stream forwarding operation completes.
        /// </summary>
        /// <remarks>On Windows, appends the configured Python 3 library directory to PATH before starting
        /// the process when a Python library path is provided.</remarks>
        /// <param name="cancellationToken">Token used to cancel asynchronous stream forwarding operations.</param>
        /// <returns>A task that represents the asynchronous execution of the process and stream forwarding pipeline.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the LLDB DAP process cannot be started.</exception>
        public Task RunAsync(CancellationToken cancellationToken)
        {
            // This method runs synchronously to ensure any errors are propagated to the caller immediately.
            // RunLoop is asynchronous.
            var startInfo = new ProcessStartInfo(this.options.LldbDapPath.FullName)
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            if (OperatingSystem.IsWindows() && this.options.Python3Library != null)
            {
                startInfo.Environment["PATH"] = $"{this.options.Python3Library.DirectoryName};{startInfo.Environment["PATH"]}";
            }

            var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException($"Failed to start {startInfo.FileName}.");

            return RunLoop(process, cancellationToken);
        }

        private async Task RunLoop(Process process, CancellationToken cancellationToken)
        {
            await Task.WhenAny(
                this.options.Input.CopyToAsync(process.StandardInput.BaseStream, cancellationToken),
                process.StandardOutput.BaseStream.CopyToAsync(this.options.Output, cancellationToken),
                process.StandardError.BaseStream.CopyToAsync(this.options.Error, cancellationToken));

            process.Kill(entireProcessTree: true);
        }
    }
}
