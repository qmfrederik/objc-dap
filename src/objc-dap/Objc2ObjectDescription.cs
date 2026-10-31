using Objc.Dap.Protocol;

namespace Objc.Dap
{
    /// <summary>
    /// What is known about an object once it has been read from the debuggee, ready to be applied to a variable.
    /// </summary>
    internal sealed class Objc2ObjectDescription
    {
        public string? Type { get; init; }

        public string? Value { get; init; }

        public string? MemoryReference { get; init; }

        // Zero if the object has nothing to expand.
        public int VariablesReference { get; init; }

        public int IndexedVariables { get; init; }

        public int NamedVariables { get; init; }

        public void ApplyTo(Variable variable)
        {
            variable.Type = this.Type ?? variable.Type;
            variable.Value = this.Value ?? variable.Value;
            variable.MemoryReference = this.MemoryReference ?? variable.MemoryReference;

            if (this.VariablesReference != 0)
            {
                variable.VariablesReference = this.VariablesReference;
                variable.IndexedVariables = this.IndexedVariables;
                variable.NamedVariables = this.NamedVariables;
            }
        }
    }
}
