using Microsoft.Extensions.Logging;
using Objc.Dap.Protocol;
using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json;

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
                this.DapProxyLoop(this.options.Input, process.StandardInput.BaseStream, cancellationToken),
                this.DapProxyLoop(process.StandardOutput.BaseStream, this.options.Output, cancellationToken),
                process.StandardError.BaseStream.CopyToAsync(this.options.Error, cancellationToken));

            process.Kill(entireProcessTree: true);
        }

        //Asynchronously reads Debug Adapter Protocol messages from a source stream, parses framed JSON payloads, and
        /// advances the reader as messages are consumed.
        /// <remarks>Expects DAP framing with a single Content-Length header, followed by \r\n\r\n and a
        /// UTF-8 JSON payload.</remarks>Stream that provides incoming protocol data.Stream intended to receive proxied protocol data.Token that cancels pending reads and stops the loop.A task that completes when the source stream is completed or cancellation is requested.
        private async Task DapProxyLoop(Stream source, Stream target, CancellationToken cancellationToken)
        {
            var reader = PipeReader.Create(source);

            while (true)
            {
                var result = await reader.ReadAsync(cancellationToken);
                if (result.IsCompleted)
                {
                    return;
                }

                var buffer = result.Buffer;

                // Only a single header is supported, i.e. finding the first '\r` character will get us
                // to the end of Content-Length:
                // Content-Length: 119\r\n
                // \r\n
                // {
                //     "seq": 153,
                //     "type": "request",
                //     "command": "next",
                //     "arguments": {
                //         "threadId": 3
                //     }
                // }
                SequencePosition? position = buffer.PositionOf((byte)'\r');

                if (position != null)
                {
                    var header = Encoding.ASCII.GetString(buffer.Slice(0, position.Value));
                    var length = int.Parse(header.Substring(16, header.Length - 16));

                    if (buffer.Length >= header.Length + 4 /* \r\n\r\n */ + length)
                    {
                        var message = buffer.Slice(header.Length + 4, length);
                        var messageWithHeader = buffer.Slice(0, header.Length + 4 + length);

                        var jsonReader = new Utf8JsonReader(message);
                        var value = JsonSerializer.Deserialize(ref jsonReader, DebugAdapterSerializerContext.Default.ProtocolMessage)!;

                        // Reset the reader
                        jsonReader = new Utf8JsonReader(message);

                        switch (value.Type)
                        {
                            case ProtocolMessageType.Request:
                                var request = JsonSerializer.Deserialize(ref jsonReader, DebugAdapterSerializerContext.Default.Request)!;
                                await this.ProxyRequest(messageWithHeader, message, request, target, cancellationToken);
                                break;

                            case ProtocolMessageType.Response:
                                var response = JsonSerializer.Deserialize(ref jsonReader, DebugAdapterSerializerContext.Default.Response)!;
                                await this.ProxyResponse(messageWithHeader, message, response, target, cancellationToken);
                                break;

                            case ProtocolMessageType.Event:
                                var @event = JsonSerializer.Deserialize(ref jsonReader, DebugAdapterSerializerContext.Default.Event)!;
                                await this.ProxyEvent(messageWithHeader, message, @event, target, cancellationToken);
                                break;

                            default:
                                await this.ProxyMessage(messageWithHeader, message, target, cancellationToken);
                                break;
                        }

                        reader.AdvanceTo(message.End);
                        continue;
                    }
                }

                reader.AdvanceTo(buffer.Start, buffer.End);
            }
        }

        protected virtual Task ProxyRequest(ReadOnlySequence<byte> messageWithHeader, ReadOnlySequence<byte> message, Request request, Stream target, CancellationToken cancellationToken)
        {
            return this.ProxyMessage(messageWithHeader, message, target, cancellationToken);
        }

        protected virtual Task ProxyResponse(ReadOnlySequence<byte> messageWithHeader, ReadOnlySequence<byte> message, Response response, Stream target, CancellationToken cancellationToken)
        {
            return this.ProxyMessage(messageWithHeader, message, target, cancellationToken);
        }

        protected virtual Task ProxyEvent(ReadOnlySequence<byte> messageWithHeader, ReadOnlySequence<byte> message, Event @event, Stream target, CancellationToken cancellationToken)
        {
            return this.ProxyMessage(messageWithHeader, message, target, cancellationToken);
        }

        protected virtual async Task ProxyMessage(ReadOnlySequence<byte> messageWithHeader, ReadOnlySequence<byte> message, Stream target, CancellationToken cancellationToken)
        {
            foreach (var span in messageWithHeader)
            {
                await target.WriteAsync(span, cancellationToken);
            }
        }
    }
}
