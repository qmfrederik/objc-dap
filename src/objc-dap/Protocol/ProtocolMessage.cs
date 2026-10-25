using System.Text.Json.Serialization;

namespace Objc.Dap.Protocol
{
    public class ProtocolMessage
    {
        /// <summary>
        /// Gets or sets sequence number of the message (also known as message ID). The `seq` for
        /// the first message sent by a client or debug adapter is 1, and for each
        /// subsequent message is 1 greater than the previous message sent by that
        /// actor. `seq` can be used to order requests, responses, and events, and to
        /// associate requests with their corresponding responses.For protocol
        /// messages of type `request` the sequence number can be used to cancel the
        /// request.
        /// </summary>
        [JsonPropertyName("seq")]
        public int Sequence { get; set; }

        [JsonPropertyName("type")]
        public virtual ProtocolMessageType Type { get; set; }
    }
}
