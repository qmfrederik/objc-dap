namespace Objc.Dap.Runtime
{
    public record Objc2InstanceVariable
    {
        private static readonly char[] TypeQualifiers = ['r', 'n', 'N', 'o', 'O', 'R', 'V'];

        public required string Name { get; init; }

        public required string TypeEncoding { get; init; }

        /// <summary>
        /// Gets the offset, in bytes, of the instance variable from the start of the object.
        /// </summary>
        public int Offset { get; init; }

        public uint Size { get; init; }

        /// <summary>
        /// Gets the first character of the type encoding, ignoring qualifiers such as <c>r</c> (const).
        /// </summary>
        public char EncodingKind
        {
            get
            {
                var encoding = this.TypeEncoding.TrimStart(TypeQualifiers);
                return encoding.Length > 0 ? encoding[0] : '\0';
            }
        }

        /// <summary>
        /// Gets a C-style name for the type, falling back to the raw encoding for types without a simple name.
        /// </summary>
        public string TypeName
        {
            get
            {
                var encoding = this.TypeEncoding.TrimStart(TypeQualifiers);

                return this.EncodingKind switch
                {
                    'c' => "char",
                    'C' => "unsigned char",
                    's' => "short",
                    'S' => "unsigned short",
                    'i' => "int",
                    'I' => "unsigned int",
                    'l' => "long",
                    'L' => "unsigned long",
                    'q' => "long long",
                    'Q' => "unsigned long long",
                    'f' => "float",
                    'd' => "double",
                    'B' => "bool",
                    '#' => "Class",
                    ':' => "SEL",
                    '*' => "char *",
                    '@' => GetObjectTypeName(encoding),
                    _ => this.TypeEncoding,
                };
            }
        }

        // Object encodings are "@", "@?" (block) or "@\"ClassName\"" / "@\"<Protocol>\"".
        private static string GetObjectTypeName(string encoding)
        {
            if (encoding == "@?")
            {
                return "block";
            }

            if (encoding.Length > 3 && encoding[1] == '"' && encoding[^1] == '"')
            {
                var name = encoding.Substring(2, encoding.Length - 3);
                return name.StartsWith('<') ? $"id{name}" : $"{name} *";
            }

            return "id";
        }
    }
}
