using System.Text.Json.Serialization;

namespace Objc.Dap.Protocol
{
    internal class VariablePresentationHint
    {
        /// <summary>
        /// Gets or sets the kind of variable. Before introducing additional values, try to use the
        /// listed values.
        /// </summary>
        /// <remarks>
        /// Values:
        /// 'property': Indicates that the object is a property.
        /// 'method': Indicates that the object is a method.
        /// 'class': Indicates that the object is a class.
        /// 'data': Indicates that the object is data.
        /// 'event': Indicates that the object is an event.
        /// 'baseClass': Indicates that the object is a base class.
        /// 'innerClass': Indicates that the object is an inner class.
        /// 'interface': Indicates that the object is an interface.
        /// 'mostDerivedClass': Indicates that the object is the most derived class.
        /// 'virtual': Indicates that the object is virtual, that means it is a
        /// synthetic object introduced by the adapter for rendering purposes, e.g. an
        /// index range for large arrays.
        /// 'dataBreakpoint': Deprecated: Indicates that a data breakpoint is
        /// registered for the object. The <c>hasDataBreakpoint</c> attribute should
        /// generally be used instead.
        /// etc.
        /// </remarks>
        [JsonPropertyName("kind")]
        public string? Kind { get; set; }

        /// <summary>
        /// Gets or sets the set of attributes represented as an array of strings. Before introducing
        /// additional values, try to use the listed values.
        /// </summary>
        /// <remarks>
        /// Values:
        /// 'static': Indicates that the object is static.
        /// 'constant': Indicates that the object is a constant.
        /// 'readOnly': Indicates that the object is read only.
        /// 'rawString': Indicates that the object is a raw string.
        /// 'hasObjectId': Indicates that the object can have an Object ID created for
        /// it. This is a vestigial attribute that is used by some clients; 'Object
        /// ID's are not specified in the protocol.
        /// 'canHaveObjectId': Indicates that the object has an Object ID associated
        /// with it. This is a vestigial attribute that is used by some clients;
        /// 'Object ID's are not specified in the protocol.
        /// 'hasSideEffects': Indicates that the evaluation had side effects.
        /// 'hasDataBreakpoint': Indicates that the object has its value tracked by a
        /// data breakpoint.
        /// etc.
        /// </remarks>
        [JsonPropertyName("attributes")]
        public string[]? Attributes { get; set; }

        /// <summary>
        /// Gets or sets the visibility of variable. Before introducing additional values, try to use
        /// the listed values.
        /// </summary>
        /// <remarks>
        /// Values: 'public', 'private', 'protected', 'internal', 'final', etc.
        /// </remarks>
        [JsonPropertyName("visibility")]
        public string? Visibility { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether clients can present the variable with a UI that
        /// supports a specific gesture to trigger its evaluation.
        /// </summary>
        /// <remarks>
        /// This mechanism can be used for properties that require executing code when
        /// retrieving their value and where the code execution can be expensive and/or
        /// produce side-effects. A typical example are properties based on a getter
        /// function.
        /// Please note that in addition to the <c>lazy</c> flag, the variable's
        /// <c>variablesReference</c> is expected to refer to a variable that will provide
        /// the value through another <c>variable</c> request.
        /// </remarks>
        [JsonPropertyName("lazy")]
        public bool? Lazy { get; set; }
    }
}
