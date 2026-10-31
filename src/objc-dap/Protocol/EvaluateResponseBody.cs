using System.Text.Json.Serialization;

namespace Objc.Dap.Protocol
{
    public class EvaluateResponseBody
    {
        /// <summary>
        /// Gets or sets the result of the evaluate request as a string.
        /// </summary>
        [JsonPropertyName("result")]
        public string Result { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the type of the evaluate result.
        /// </summary>
        /// <remarks>
        /// This attribute should only be returned by a debug adapter if the
        /// corresponding capability <c>supportsVariableType</c> is true.
        /// </remarks>
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        /// <summary>
        /// Gets or sets properties of an evaluate result that can be used to determine how to render
        /// the result in the UI.
        /// </summary>
        [JsonPropertyName("presentationHint")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public VariablePresentationHint? PresentationHint { get; set; }

        /// <summary>
        /// Gets or sets the reference used to retrieve the result's children.
        /// </summary>
        /// <remarks>
        /// If <c>variablesReference</c> is &gt; 0, the evaluate result is structured and its
        /// children can be retrieved by passing <c>variablesReference</c> to the <c>variables</c>
        /// request as long as execution remains suspended.
        /// </remarks>
        [JsonPropertyName("variablesReference")]
        public int VariablesReference { get; set; }

        /// <summary>
        /// Gets or sets the number of named child variables.
        /// </summary>
        [JsonPropertyName("namedVariables")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int NamedVariables { get; set; }

        /// <summary>
        /// Gets or sets the number of indexed child variables.
        /// </summary>
        [JsonPropertyName("indexedVariables")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int IndexedVariables { get; set; }

        /// <summary>
        /// Gets or sets a memory reference to a location appropriate for this result.
        /// </summary>
        /// <remarks>
        /// For pointer type eval results, this is generally a reference to the memory address
        /// contained in the pointer.
        /// This attribute may be returned by a debug adapter if corresponding capability
        /// <c>supportsMemoryReferences</c> is true.
        /// </remarks>
        [JsonPropertyName("memoryReference")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? MemoryReference { get; set; }

        /// <summary>
        /// Gets or sets a reference that allows the client to request the location where the
        /// returned value is declared.
        /// </summary>
        [JsonPropertyName("valueLocationReference")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int ValueLocationReference { get; set; }
    }
}
