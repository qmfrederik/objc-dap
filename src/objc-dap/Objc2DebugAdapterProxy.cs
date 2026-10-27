using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Threading;
using Objc.Dap.Protocol;
using Objc.Dap.Runtime;
using System.Buffers;
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
            if (message is VariablesResponse)
            {
                var variablesResponse = (VariablesResponse)message;
                var variablesResponseMessage = this.CreateMessage(variablesResponse, DebugAdapterSerializerContext.Default.Response);
                var variablesResponseString = Encoding.UTF8.GetString(variablesResponseMessage.Value.Span);

                var inputString = Encoding.UTF8.GetString(messageWithHeader);
                target.Enqueue(variablesResponseMessage);
            }
            else
            {
                base.ProxyMessage(messageWithHeader, messageBytes, message, target);
            }
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
