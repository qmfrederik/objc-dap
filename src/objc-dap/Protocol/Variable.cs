using System.Text.Json.Serialization;

namespace Objc.Dap.Protocol
{
    internal class Variable
    {
        /// <summary>
        /// Gets or sets the variable's name.
        /// </summary>
        [JsonPropertyName("name")]
        public required string Name { get; set; }

        /// <summary>
        /// Gets or sets the variable's value.
        /// </summary>
        /// <remarks>
        /// This can be a multi-line text, e.g. for a function the body of a function.
        /// For structured variables (which do not have a simple value), it is
        /// recommended to provide a one-line representation of the structured object.
        /// This helps to identify the structured object in the collapsed state when
        /// its children are not yet visible.
        /// An empty string can be used if no value should be shown in the UI.
        /// </remarks>
        [JsonPropertyName("value")]
        public required string Value { get; set; }

        /// <summary>
        /// Gets or sets the type of the variable's value. Typically shown in the UI when hovering
        /// over the value.
        /// </summary>
        /// <remarks>
        /// This attribute should only be returned by a debug adapter if the
        /// corresponding capability <c>supportsVariableType</c> is true.
        /// </remarks>
        [JsonPropertyName("type")]
        public required string Type { get; set; }

        /// <summary>
        /// Gets or sets properties of a variable that can be used to determine how to render the
        /// variable in the UI.
        /// </summary>
        [JsonPropertyName("presentationHint")]
        public VariablePresentationHint? PresentationHint { get; set; }

        /// <summary>
        /// Gets or sets the evaluatable name of this variable which can be passed to the
        /// <c>evaluate</c> request to fetch the variable's value.
        /// </summary>
        [JsonPropertyName("evaluateName")]
        public string? EvaluateName { get; set; }

        /// <summary>
        /// Gets or sets the reference used to retrieve the variable's children.
        /// </summary>
        /// <remarks>
        /// If <c>variablesReference</c> is &gt; 0, the variable is structured and its children
        /// can be retrieved by passing <c>variablesReference</c> to the <c>variables</c> request
        /// as long as execution remains suspended. See 'Lifetime of Object References'
        /// in the Overview section for details.
        /// </remarks>
        [JsonPropertyName("variablesReference")]
        public int VariablesReference { get; set; }

        /// <summary>
        /// Gets or sets the number of named child variables.
        /// </summary>
        /// <remarks>
        /// The client can use this information to present the children in a paged UI
        /// and fetch them in chunks.
        /// </remarks>
        [JsonPropertyName("namedVariables")]
        public int NamedVariables { get; set; }

        /// <summary>
        /// Gets or sets the number of indexed child variables.
        /// </summary>
        /// <remarks>
        /// The client can use this information to present the children in a paged UI
        /// and fetch them in chunks.
        /// </remarks>
        [JsonPropertyName("indexedVariables")]
        public int IndexedVariables { get; set; }

        /// <summary>
        /// Gets or sets a memory reference associated with this variable.
        /// </summary>
        /// <remarks>
        /// For pointer type variables, this is generally a reference to the memory
        /// address contained in the pointer.
        /// For executable data, this reference may later be used in a <c>disassemble</c>
        /// request.
        /// This attribute may be returned by a debug adapter if corresponding
        /// capability <c>supportsMemoryReferences</c> is true.
        /// </remarks>
        [JsonPropertyName("memoryReference")]
        public string? MemoryReference { get; set; }

        /// <summary>
        /// Gets or sets a reference that allows the client to request the location where the
        /// variable is declared.
        /// </summary>
        /// <remarks>
        /// This should be present only if the adapter is likely to be able to resolve the location.
        /// This reference shares the same lifetime as the <c>variablesReference</c>. See
        /// 'Lifetime of Object References' in the Overview section for details.
        /// </remarks>
        [JsonPropertyName("declarationReference")]
        public int DeclarationReference { get; set; }

        /// <summary>
        /// Gets or sets a reference that allows the client to request the location where the
        /// variable's value is declared.
        /// </summary>
        /// <remarks>
        /// For example, if the variable contains a function pointer, the adapter may be able to
        /// look up the function's location. This should be present only if the adapter is likely
        /// to be able to resolve the location.
        /// This reference shares the same lifetime as the <c>variablesReference</c>. See
        /// 'Lifetime of Object References' in the Overview section for details.
        /// </remarks>
        [JsonPropertyName("valueDeclarationReference")]
        public int ValueDeclarationReference { get; set; }
    }
}
