using System.Text.Json.Serialization;

namespace Objc.Dap.Protocol
{
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "command", IgnoreUnrecognizedTypeDiscriminators = true)]
    [JsonDerivedType(typeof(VariablesResponse), VariablesResponse.CommandName)]
    public class Response : ProtocolMessage
    {
        [JsonPropertyName("type")]
        public override ProtocolMessageType Type => ProtocolMessageType.Response;

        /// <summary>
        /// Gets or sets sequence number of the corresponding request.
        /// </summary>
        [JsonPropertyName("request_seq")]
        public int RequestSequence { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether outcome of the request.
        /// If true, the request was successful and the `body` attribute may contain
        /// the result of the request.
        /// If the value is false, the attribute `message` contains the error in short
        /// form and the `body` may contain additional information (see
        /// `ErrorResponse.body.error`).
        /// </summary>
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets contains the raw error in short form if `success` is false.
        /// This raw error might be interpreted by the client and is not shown in the
        /// UI.
        /// Some predefined values exist.
        /// Values:
        /// 'cancelled': the request was cancelled.
        /// 'notStopped': the request may be retried once the adapter is in a 'stopped'
        /// state.
        /// etc.
        /// </summary>
        [JsonPropertyName("message")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Message { get; set; }
    }
}
