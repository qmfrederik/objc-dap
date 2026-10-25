using System.Text.Json.Serialization;

namespace Objc.Dap.Protocol
{
    internal class VariablesResponse : Response
    {
        public const string CommandName = "variables";

        [JsonPropertyName("body")]
        public required VariablesResponseBody Body { get; set; }
    }
}
