using Objc.Dap.Protocol;
using System.Buffers;

namespace Objc.Dap
{
    public record Message : IDisposable
    {
        public Message(IMemoryOwner<byte> owner, Memory<byte> value, ProtocolMessageType type, ProtocolMessage payload)
        {
            this.owner = owner;
            this.Value = value;
            this.Type = type;
            this.Payload = payload;
        }

        private IMemoryOwner<byte> owner;

        public Memory<byte> Value { get; private init; }

        public ProtocolMessageType Type { get; init; }

        public ProtocolMessage? Payload { get; private init; }

        public void Dispose()
        {
            this.owner.Dispose();
        }
    }
}
