using System.Text.Json.Serialization;

namespace Objc.Dap.Protocol
{
    public class VariablesArguments
    {
        /// <summary>
        /// Gets or sets the reference of the variable container.
        /// </summary>
        [JsonPropertyName("variablesReference")]
        public int VariablesReference { get; set; }

        /// <summary>
        /// Gets or sets the type of the child variables to return, either <c>indexed</c> or <c>named</c>.
        /// If omitted, both types are fetched.
        /// </summary>
        [JsonPropertyName("filter")]
        public string? Filter { get; set; }

        /// <summary>
        /// Gets or sets the index of the first variable to return. If omitted, children start at 0.
        /// </summary>
        [JsonPropertyName("start")]
        public int? Start { get; set; }

        /// <summary>
        /// Gets or sets the number of variables to return. If zero or omitted, all variables are returned.
        /// </summary>
        [JsonPropertyName("count")]
        public int? Count { get; set; }
    }
}
