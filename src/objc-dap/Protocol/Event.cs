using System.Text.Json.Serialization;

namespace Objc.Dap.Protocol
{
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "event", IgnoreUnrecognizedTypeDiscriminators = true)]
    [JsonDerivedType(typeof(ProcessEvent), ProcessEvent.EventName)]
    public class Event : ProtocolMessage
    {
        [JsonPropertyName("type")]
        public override ProtocolMessageType Type => ProtocolMessageType.Event;
    }
}
