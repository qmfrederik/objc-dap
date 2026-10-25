using System.Text.Json.Serialization;

namespace Objc.Dap
{
    [JsonConverter(typeof(JsonStringEnumConverter<ProtocolMessageType>))]
    public enum ProtocolMessageType
    {
        /// <summary>
        /// Represents a protocol message type that indicates a request operation.
        /// </summary>
        [JsonStringEnumMemberName("request")]
        Request,

        /// <summary>
        /// Represents a protocol message type that indicates a response.
        /// </summary>
        [JsonStringEnumMemberName("response")]
        Response,

        /// <summary>
        /// Represents an event-type protocol message.
        /// </summary>
        [JsonStringEnumMemberName("event")]
        Event,
    }
}
