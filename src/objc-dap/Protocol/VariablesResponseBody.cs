using System.Text.Json.Serialization;

namespace Objc.Dap.Protocol
{
    internal class VariablesResponseBody
    {
        [JsonPropertyName("variables")]
        public required Variable[] Variables { get; set; }
    }
}
