using System.Text.Json.Serialization;

namespace Objc.Dap.Protocol
{
    public class VariablesRequest : Request
    {
        public const string CommandName = "variables";

        [JsonPropertyName("arguments")]
        public VariablesArguments? Arguments { get; set; }
    }
}
