using System.Text.Json.Serialization;

namespace Objc.Dap.Protocol
{
    public class EvaluateArguments
    {
        public const string HoverContext = "hover";
        public const string WatchContext = "watch";
        public const string ReplContext = "repl";
        public const string ClipboardContext = "clipboard";
        public const string VariablesContext = "variables";

        /// <summary>
        /// Gets or sets the expression to evaluate.
        /// </summary>
        [JsonPropertyName("expression")]
        public string Expression { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the stack frame in which to evaluate the expression.
        /// If not specified, the expression is evaluated in the global scope.
        /// </summary>
        [JsonPropertyName("frameId")]
        public int? FrameId { get; set; }

        /// <summary>
        /// Gets or sets the context in which the evaluate request is used.
        /// </summary>
        /// <remarks>
        /// Values: <c>watch</c>, <c>repl</c>, <c>hover</c>, <c>clipboard</c>, <c>variables</c>, etc.
        /// </remarks>
        [JsonPropertyName("context")]
        public string? Context { get; set; }
    }
}
