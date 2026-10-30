using System.Text.Json.Serialization;

namespace Objc.Dap.Protocol
{
    public class VariablesRequest : Request
    {
        [JsonPropertyName("arguments")]
        public VariablesArguments? Arguments { get; set; }
    }
}
