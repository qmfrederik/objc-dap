using System.Buffers;
using System.Text;
using System.Text.Json;

namespace Objc.Dap
{
    public static class MessageSummary
    {
        /// <summary>
        /// Describes a DAP message, e.g. <c>request command=variables seq=12</c>, <c>response command=variables seq=5 request_seq=12</c>
        /// or <c>event event=stopped seq=7</c>,
        /// by scanning the top-level properties of its JSON body.
        /// </summary>
        /// <param name="body">The JSON body of the message, without the <c>Content-Length</c> header.</param>
        public static string Describe(ReadOnlySequence<byte> body)
        {
            string? type = null;
            string? command = null;
            string? @event = null;
            int? seq = null;
            int? requestSeq = null;

            try
            {
                var reader = new Utf8JsonReader(body);

                if (reader.Read() && reader.TokenType == JsonTokenType.StartObject)
                {
                    while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
                    {
                        if (reader.ValueTextEquals("type"u8))
                        {
                            reader.Read();
                            type = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
                        }
                        else if (reader.ValueTextEquals("command"u8))
                        {
                            reader.Read();
                            command = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
                        }
                        else if (reader.ValueTextEquals("event"u8))
                        {
                            reader.Read();
                            @event = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
                        }
                        else if (reader.ValueTextEquals("seq"u8))
                        {
                            reader.Read();
                            seq = reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var value) ? value : null;
                        }
                        else if (reader.ValueTextEquals("request_seq"u8))
                        {
                            reader.Read();
                            requestSeq = reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var value) ? value : null;
                        }
                        else
                        {
                            reader.Read();
                            reader.Skip();
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // Return whatever was found before the malformed JSON.
            }

            var summary = new StringBuilder(type ?? "unknown");

            if (command is not null)
            {
                summary.Append(" command=").Append(command);
            }

            if (@event is not null)
            {
                summary.Append(" event=").Append(@event);
            }

            if (seq is not null)
            {
                summary.Append(" seq=").Append(seq.Value);
            }

            if (requestSeq is not null)
            {
                summary.Append(" request_seq=").Append(requestSeq.Value);
            }

            return summary.ToString();
        }
    }
}
