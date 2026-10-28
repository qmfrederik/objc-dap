using System.Text.Json.Serialization;

namespace Objc.Dap.Protocol
{
    public class ProcessEvent : Event
    {
        public const string EventName = "process";

        [JsonPropertyName("body")]
        public required ProcessEventBody Body { get; set; }
    }
}
