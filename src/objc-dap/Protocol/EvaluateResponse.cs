using System.Text.Json.Serialization;

namespace Objc.Dap.Protocol
{
    public class EvaluateResponse : Response
    {
        public const string CommandName = EvaluateRequest.CommandName;

        // Failed evaluations carry an error body of a different shape, and may have none at all.
        [JsonPropertyName("body")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public EvaluateResponseBody? Body { get; set; }
    }
}
