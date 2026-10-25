using System.Text.Json.Serialization;

namespace Objc.Dap.Protocol
{
    internal class ProcessEvent : Event
    {
        public const string EventName = "process";

        [JsonPropertyName("body")]
        public required ProcessEventBody Body { get; set; }
    }
}
