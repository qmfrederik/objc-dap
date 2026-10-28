using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Threading;
using Objc.Dap.Protocol;
using Objc.Dap.Runtime;
using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Objc.Dap
{
    public class Objc2DebugAdapterProxy : DebugAdapterProxy
    {
        private readonly MemoryPool<byte> memoryPool = MemoryPool<byte>.Shared;

        private readonly Objc2Runtime runtime;

        public Objc2DebugAdapterProxy(DebugAdapterOptions options, Objc2Runtime runtime, ILogger<DebugAdapterProxy> logger)
            : base(options, logger)
        {
            this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        }

        protected override void ProxyMessage(ReadOnlySequence<byte> messageWithHeader, ReadOnlySequence<byte> messageBytes, ProtocolMessage message, AsyncQueue<Message?> target)
        {
            if (message is ProcessEvent)
            {
                var processEvent = (ProcessEvent)message;
                this.runtime.Open(processEvent.Body.SystemProcessId!.Value);
                base.ProxyMessage(messageWithHeader, messageBytes, message, target);
            }
            else if (message is VariablesResponse)
            {
                var variablesResponse = (VariablesResponse)message;

                var variables = variablesResponse.Body.Variables;

                foreach (var variable in variables)
                {
                    if (variable.Type.EndsWith(" *")
                        && variable.Value != null
                        && variable.Value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    {
                        // Try to read te
                        var reference = GetValueAddress(variable);
                        var @object = this.runtime.ReadObject((nuint)reference);

                        if (@object == Objc2Object.Null)
                        {
                            continue;
                        }

                        var className = this.runtime.GetClassName(@object);

                        if (className != null)
                        {
                            variable.Type = $"{className} *";
                        }

                        var description = this.runtime.GetObjectDescription(@object);

                        if (description != null)
                        {
                            variable.Value = description;
                        }
                    }
                }

                var variablesResponseMessage = this.CreateMessage(variablesResponse, DebugAdapterSerializerContext.Default.Response);
#if DEBUG
                var variablesResponseString = Encoding.UTF8.GetString(variablesResponseMessage.Value.Span);
                var inputString = Encoding.UTF8.GetString(messageWithHeader);
#endif
                target.Enqueue(variablesResponseMessage);
            }
            else
            {
                base.ProxyMessage(messageWithHeader, messageBytes, message, target);
            }
        }

        private static ulong GetValueAddress(Variable variable)
        {
            ReadOnlySpan<char> reference = variable.Value;
            if (reference.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                reference = reference.Slice(2);
            }

            // A missing or malformed reference maps to the null address.
            return ulong.TryParse(reference, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ulong address) ? address : 0;
        }

        private Message CreateMessage<T>(T message, JsonTypeInfo<T> jsonTypeInfo)
            where T : ProtocolMessage
        {
            using (MemoryStream stream = new MemoryStream())
            {
                JsonSerializer.Serialize(stream, message, jsonTypeInfo);
                var header = $"Content-Length: {stream.Length}\r\n\r\n";
                var headerLength = Encoding.ASCII.GetByteCount(header);

                var memoryOwner = this.memoryPool.Rent(headerLength + (int)stream.Length);
                var memory = memoryOwner.Memory.Slice(0, headerLength + (int)stream.Length);

                Encoding.ASCII.GetBytes(header, memory.Slice(0, headerLength).Span);
                stream.Position = 0;
                stream.ReadExactly(memory.Slice(headerLength, (int)stream.Length).Span);

                return new Message(memoryOwner, memory, message.Type, message);
            }
        }
    }
}
