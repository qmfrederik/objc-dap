using System.Text.Json;
using System.Text.Json.Serialization;

namespace Objc.Dap.Protocol
{
    /// <summary>
    /// Reads a request as the class which represents its command, if there is one, and as a plain <see cref="Request"/> otherwise.
    /// </summary>
    /// <remarks>
    /// <see cref="Request.Command"/> is a property, which stops System.Text.Json from using it as the discriminator
    /// of a polymorphic type. Reading the command here allows requests without a class of their own to keep it.
    /// </remarks>
    public sealed class RequestConverter : JsonConverter<Request>
    {
        public override Request? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            var root = document.RootElement;

            var command = root.TryGetProperty("command", out var commandElement) ? commandElement.GetString() : null;

            var type = command switch
            {
                VariablesRequest.CommandName => typeof(VariablesRequest),
                EvaluateRequest.CommandName => typeof(EvaluateRequest),
                _ => null,
            };

            if (type != null)
            {
                return (Request?)root.Deserialize(options.GetTypeInfo(type));
            }

            // Deserializing a Request would end up in this converter again.
            return new Request()
            {
                Sequence = root.TryGetProperty("seq", out var sequence) ? sequence.GetInt32() : 0,
                Command = command ?? string.Empty,
            };
        }

        public override void Write(Utf8JsonWriter writer, Request value, JsonSerializerOptions options)
        {
            if (value.GetType() == typeof(Request))
            {
                throw new NotSupportedException("Requests without a class of their own are only read.");
            }

            JsonSerializer.Serialize(writer, value, options.GetTypeInfo(value.GetType()));
        }
    }
}
