using System.Text.Json.Serialization;

namespace Objc.Dap.Protocol
{
    public class EvaluateRequest : Request
    {
        public const string CommandName = "evaluate";

        [JsonPropertyName("arguments")]
        public EvaluateArguments? Arguments { get; set; }
    }
}
