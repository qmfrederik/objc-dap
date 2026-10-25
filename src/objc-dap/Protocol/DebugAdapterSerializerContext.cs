using System.Text.Json.Serialization;

namespace Objc.Dap.Protocol
{
    [JsonSourceGenerationOptions(AllowOutOfOrderMetadataProperties = true)]
    [JsonSerializable(typeof(ProtocolMessage))]
    [JsonSerializable(typeof(Request))]
    [JsonSerializable(typeof(Response))]
    [JsonSerializable(typeof(Event))]
    internal partial class DebugAdapterSerializerContext : JsonSerializerContext
    {
    }
}
