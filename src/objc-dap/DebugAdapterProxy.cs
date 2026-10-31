using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Threading;
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
        private readonly MemoryPool<byte> memoryPool = MemoryPool<byte>.Shared;

        // Messages which are about to be proxied are stored on these queues, allowing multiple threads to post
        // messages in parallel.
        private readonly AsyncQueue<Message?> serverQueue = new AsyncQueue<Message?>();
        private readonly AsyncQueue<Message?> clientQueue = new AsyncQueue<Message?>();

        /// <summary>
        /// Gets the queue of messages destined for the client.
        /// </summary>
        protected AsyncQueue<Message?> ClientQueue => this.clientQueue;

        /// <summary>
        /// Gets the queue of messages destined for <c>lldb-dap</c>.
        /// </summary>
        protected AsyncQueue<Message?> ServerQueue => this.serverQueue;

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

            return RunLoopAsync(process, cancellationToken);
        }

        private async Task RunLoopAsync(Process process, CancellationToken cancellationToken)
        {
            await Task.WhenAny(
                this.DapProxyLoopAsync(this.options.Input, this.serverQueue, cancellationToken),
                this.DapProxyLoopAsync(this.serverQueue, process.StandardInput.BaseStream, cancellationToken),
                this.DapProxyLoopAsync(process.StandardOutput.BaseStream, this.clientQueue, cancellationToken),
                this.DapProxyLoopAsync(this.clientQueue, this.options.Output, cancellationToken),
                process.StandardError.BaseStream.CopyToAsync(this.options.Error, cancellationToken));

            process.Kill(entireProcessTree: true);
        }

        private async Task DapProxyLoopAsync(AsyncQueue<Message?> queue, Stream target, CancellationToken cancellationToken)
        {
            Message? message;

            while ((message = await queue.DequeueAsync(cancellationToken)) != null)
            {
                await target.WriteAsync(message.Value, cancellationToken);
                message.Dispose();
            }
        }

        //Asynchronously reads Debug Adapter Protocol messages from a source stream, parses framed JSON payloads, and
        /// advances the reader as messages are consumed.
        /// <remarks>Expects DAP framing with a single Content-Length header, followed by \r\n\r\n and a
        /// UTF-8 JSON payload.</remarks>Stream that provides incoming protocol data.Stream intended to receive proxied protocol data.Token that cancels pending reads and stops the loop.A task that completes when the source stream is completed or cancellation is requested.
        private async Task DapProxyLoopAsync(Stream source, AsyncQueue<Message?> target, CancellationToken cancellationToken)
        {
            var reader = PipeReader.Create(source);

            while (true)
            {
                var result = await reader.ReadAsync(cancellationToken);
                if (result.IsCompleted)
                {
                    target.Enqueue(null);
                    return;
                }

                var buffer = result.Buffer;

                // Only a single header is supported, i.e. finding the first '\r' character will get us
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
                                this.ProxyRequest(messageWithHeader, message, request, target);
                                break;

                            case ProtocolMessageType.Response:
                                var response = JsonSerializer.Deserialize(ref jsonReader, DebugAdapterSerializerContext.Default.Response)!;
                                this.ProxyResponse(messageWithHeader, message, response, target);
                                break;

                            case ProtocolMessageType.Event:
                                var @event = JsonSerializer.Deserialize(ref jsonReader, DebugAdapterSerializerContext.Default.Event)!;
                                this.ProxyEvent(messageWithHeader, message, @event, target);
                                break;

                            default:
                                this.ProxyMessage(messageWithHeader, message, value, target);
                                break;
                        }

                        reader.AdvanceTo(message.End);
                        continue;
                    }
                }

                reader.AdvanceTo(buffer.Start, buffer.End);
            }
        }

        protected void ProxyRequest(ReadOnlySequence<byte> messageWithHeader, ReadOnlySequence<byte> messageBytes, Request request, AsyncQueue<Message?> target)
        {
            this.ProxyMessage(messageWithHeader, messageBytes, request, target);
        }

        protected void ProxyResponse(ReadOnlySequence<byte> messageWithHeader, ReadOnlySequence<byte> messageBytes, Response response, AsyncQueue<Message?> target)
        {
            this.ProxyMessage(messageWithHeader, messageBytes, response, target);
        }

        protected void ProxyEvent(ReadOnlySequence<byte> messageWithHeader, ReadOnlySequence<byte> messageBytes, Event @event, AsyncQueue<Message?> target)
        {
            this.ProxyMessage(messageWithHeader, messageBytes, @event, target);
        }

        protected virtual void ProxyMessage(ReadOnlySequence<byte> messageWithHeader, ReadOnlySequence<byte> messageBytes, ProtocolMessage message, AsyncQueue<Message?> target)
        {
            var owner = this.memoryPool.Rent((int)messageWithHeader.Length);
            var messageMemory = owner.Memory.Slice(0, (int)messageWithHeader.Length);

            messageWithHeader.CopyTo(messageMemory.Span);

            target.Enqueue(
                new Message(owner, messageMemory, message.Type, message));
        }
    }
}
