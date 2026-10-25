using System.Text.Json.Serialization;

namespace Objc.Dap.Protocol
{
    public class Request : ProtocolMessage
    {
        [JsonPropertyName("type")]
        public override ProtocolMessageType Type => ProtocolMessageType.Request;
    }
}
