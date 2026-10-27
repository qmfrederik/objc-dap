using System.Text.Json.Serialization;

namespace Objc.Dap.Protocol
{
    [JsonSourceGenerationOptions(AllowOutOfOrderMetadataProperties = true)]
    [JsonSerializable(typeof(ProtocolMessage))]
    [JsonSerializable(typeof(Request))]
    [JsonSerializable(typeof(Response))]
    [JsonSerializable(typeof(Event))]
    public partial class DebugAdapterSerializerContext : JsonSerializerContext
    {
    }
}
