using System.Text.Json.Serialization;

namespace Objc.Dap
{
    [JsonSourceGenerationOptions(AllowOutOfOrderMetadataProperties = true)]
    [JsonSerializable(typeof(ProtocolMessage))]
    [JsonSerializable(typeof(ProtocolMessageType))]
    internal partial class DebugAdapterSerializerContext : JsonSerializerContext
    {
    }
}
