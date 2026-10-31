using Objc.Dap.Protocol;
using System.Text.Json;

namespace ObjcDap.Tests.Protocol
{
    public class EvaluateTests
    {
        [Fact]
        public void ResponseRoundtripTest()
        {
            var input = File.ReadAllText("Protocol/EvaluateResponse.json");
            var response = JsonSerializer.Deserialize(input, DebugAdapterSerializerContext.Default.Response);

            var evaluateResponse = Assert.IsType<EvaluateResponse>(response);
            Assert.True(evaluateResponse.Success);
            Assert.NotNull(evaluateResponse.Body);
            Assert.Equal("0x00007ff6d6596020", evaluateResponse.Body.Result);
            Assert.Equal("NSString *", evaluateResponse.Body.Type);
            Assert.Equal(1, evaluateResponse.Body.VariablesReference);
            Assert.Equal("0xD3076FF958", evaluateResponse.Body.MemoryReference);

            var output = JsonSerializer.Serialize(response, DebugAdapterSerializerContext.Default.Response);
            Assert.Equal(input.Length, output.Length);
        }

        [Fact]
        public void ErrorResponseCanBeRead()
        {
            // Failed evaluations carry an error body without a result.
            var input = File.ReadAllText("Protocol/EvaluateErrorResponse.json");
            var response = JsonSerializer.Deserialize(input, DebugAdapterSerializerContext.Default.Response);

            var evaluateResponse = Assert.IsType<EvaluateResponse>(response);
            Assert.False(evaluateResponse.Success);
            Assert.Equal(9, evaluateResponse.RequestSequence);
            Assert.Equal(string.Empty, evaluateResponse.Body!.Result);
        }

        [Fact]
        public void RequestCanBeRead()
        {
            var input = """{"arguments":{"context":"hover","expression":"params","frameId":1000},"command":"evaluate","seq":5,"type":"request"}""";
            var request = JsonSerializer.Deserialize(input, DebugAdapterSerializerContext.Default.EvaluateRequest);

            Assert.NotNull(request);
            Assert.Equal(EvaluateRequest.CommandName, request.Command);
            Assert.Equal("params", request.Arguments!.Expression);
            Assert.Equal(1000, request.Arguments.FrameId);
            Assert.Equal(EvaluateArguments.HoverContext, request.Arguments.Context);
        }
    }
}
