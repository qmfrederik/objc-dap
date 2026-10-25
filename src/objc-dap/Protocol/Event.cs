using System.Text.Json.Serialization;

namespace Objc.Dap.Protocol
{
    public class Event : ProtocolMessage
    {
        [JsonPropertyName("type")]
        public override ProtocolMessageType Type => ProtocolMessageType.Event;
    }
}
