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
        // Requests after which the debuggee runs or goes away, ending the lifetime of variable references.
        private static readonly HashSet<string> ReferenceInvalidatingCommands = new HashSet<string>(StringComparer.Ordinal)
        {
            "continue",
            "next",
            "stepIn",
            "stepOut",
            "stepBack",
            "reverseContinue",
            "restartFrame",
            "goto",
            "restart",
            "terminate",
            "disconnect",
        };

        private readonly MemoryPool<byte> memoryPool = MemoryPool<byte>.Shared;

        private readonly Objc2Runtime runtime;

        private readonly VariableReferenceStore references;

        public Objc2DebugAdapterProxy(DebugAdapterOptions options, Objc2Runtime runtime, VariableReferenceStore references, ILogger<DebugAdapterProxy> logger)
            : base(options, logger)
        {
            this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            this.references = references ?? throw new ArgumentNullException(nameof(references));
        }

        protected override void ProxyMessage(ReadOnlySequence<byte> messageWithHeader, ReadOnlySequence<byte> messageBytes, ProtocolMessage message, AsyncQueue<Message?> target)
        {
            if (message is Request request)
            {
                if (ReferenceInvalidatingCommands.Contains(request.Command))
                {
                    this.InvalidateReferences();
                }

                base.ProxyMessage(messageWithHeader, messageBytes, message, target);
            }
            else if (message is ProcessEvent)
            {
                var processEvent = (ProcessEvent)message;
                this.InvalidateReferences();
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

                        var value = this.runtime.GetObjectValue(@object);

                        if (value != null)
                        {
                            variable.Value = value.ToString()!;
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

        private void InvalidateReferences() => this.references.Clear();

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
