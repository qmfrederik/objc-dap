using System.Text.Json.Serialization;

namespace Objc.Dap.Protocol
{
    [JsonConverter(typeof(RequestConverter))]
    public class Request : ProtocolMessage
    {
        [JsonPropertyName("type")]
        public override ProtocolMessageType Type => ProtocolMessageType.Request;

        [JsonPropertyName("command")]
        public string Command { get; set; } = string.Empty;
    }
}
