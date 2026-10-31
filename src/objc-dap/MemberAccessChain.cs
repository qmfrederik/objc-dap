using System.Diagnostics.CodeAnalysis;

namespace Objc.Dap
{
    /// <summary>
    /// A chain of member accesses on a variable, such as <c>object->child->name</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is deliberately not a general expression parser. It recognizes only <c>identifier ("->" identifier)+</c>, so
    /// that anything we do not fully understand (calls, indexing, casts, assignments, ...) is left to the debugger.
    /// </para>
    /// <para>
    /// See <see cref="Objc2DebugAdapterProxy"/> for why this is needed.
    /// </para>
    /// </remarks>
    public sealed class MemberAccessChain
    {
        private MemberAccessChain(string root, IReadOnlyList<string> members)
        {
            this.Root = root;
            this.Members = members;
        }

        /// <summary>
        /// Gets the name of the variable the chain starts at, e.g. <c>object</c>.
        /// </summary>
        public string Root { get; }

        /// <summary>
        /// Gets the names of the members which are accessed in turn, e.g. <c>child</c> and <c>name</c>.
        /// </summary>
        public IReadOnlyList<string> Members { get; }

        /// <summary>
        /// Parses <paramref name="text"/> as a chain of member accesses.
        /// </summary>
        /// <returns>
        /// <see langword="false"/> if <paramref name="text"/> is anything else, including a variable on its own.
        /// </returns>
        public static bool TryParse(string? text, [NotNullWhen(true)] out MemberAccessChain? chain)
        {
            chain = null;

            if (text == null)
            {
                return false;
            }

            var position = 0;

            if (!TryReadIdentifier(text, ref position, out var root))
            {
                return false;
            }

            var members = new List<string>();

            while (true)
            {
                SkipWhiteSpace(text, ref position);

                if (position == text.Length)
                {
                    break;
                }

                // The arrow is a single token, so no white space is allowed inside of it.
                if (!text.AsSpan(position).StartsWith("->"))
                {
                    return false;
                }

                position += 2;
                SkipWhiteSpace(text, ref position);

                if (!TryReadIdentifier(text, ref position, out var member))
                {
                    return false;
                }

                members.Add(member);
            }

            if (members.Count == 0)
            {
                return false;
            }

            chain = new MemberAccessChain(root, members);
            return true;
        }

        private static void SkipWhiteSpace(string text, ref int position)
        {
            while (position < text.Length && char.IsWhiteSpace(text[position]))
            {
                position++;
            }
        }

        private static bool TryReadIdentifier(string text, ref int position, [NotNullWhen(true)] out string? identifier)
        {
            SkipWhiteSpace(text, ref position);

            var start = position;

            while (position < text.Length && IsIdentifierCharacter(text[position], first: position == start))
            {
                position++;
            }

            identifier = position > start ? text.Substring(start, position - start) : null;
            return identifier != null;
        }

        private static bool IsIdentifierCharacter(char character, bool first) =>
            character is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or '_' || (!first && character is >= '0' and <= '9');
    }
}
