using Objc.Dap.Protocol;
using System.Text.Json;

namespace ObjcDap.Tests.Protocol
{
    public class VariablesResponseTests
    {
        [Fact]
        public void RountripTest()
        {
            var input = File.ReadAllText("Protocol/VariablesResponse.json");
            var variablesResponse = JsonSerializer.Deserialize(input, DebugAdapterSerializerContext.Default.VariablesResponse);
            var output = JsonSerializer.Serialize(variablesResponse, DebugAdapterSerializerContext.Default.Response);

            Assert.Equal(input.Length, output.Length);
        }
    }
}
