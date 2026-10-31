using Objc.Dap.Protocol;
using System.Text.Json;

namespace ObjcDap.Tests.Protocol
{
    public class RequestTests
    {
        [Fact]
        public void KnownCommands_AreReadAsTheirOwnClass()
        {
            var variables = JsonSerializer.Deserialize(
                """{"arguments":{"variablesReference":7},"command":"variables","seq":3,"type":"request"}""",
                DebugAdapterSerializerContext.Default.Request);

            var variablesRequest = Assert.IsType<VariablesRequest>(variables);
            Assert.Equal(VariablesRequest.CommandName, variablesRequest.Command);
            Assert.Equal(3, variablesRequest.Sequence);
            Assert.Equal(7, variablesRequest.Arguments!.VariablesReference);

            var evaluate = JsonSerializer.Deserialize(
                """{"arguments":{"context":"hover","expression":"params"},"command":"evaluate","seq":5,"type":"request"}""",
                DebugAdapterSerializerContext.Default.Request);

            var evaluateRequest = Assert.IsType<EvaluateRequest>(evaluate);
            Assert.Equal(EvaluateRequest.CommandName, evaluateRequest.Command);
            Assert.Equal(EvaluateArguments.HoverContext, evaluateRequest.Arguments!.Context);
        }

        [Fact]
        public void OtherCommands_KeepTheirCommand()
        {
            var request = JsonSerializer.Deserialize(
                """{"arguments":{"threadId":1},"command":"stepIn","seq":9,"type":"request"}""",
                DebugAdapterSerializerContext.Default.Request);

            Assert.NotNull(request);
            Assert.Equal(typeof(Request), request.GetType());
            Assert.Equal("stepIn", request.Command);
            Assert.Equal(9, request.Sequence);
        }

        [Fact]
        public void KnownCommands_CanBeWrittenAsRequests()
        {
            var input = """{"arguments":{"context":"watch","expression":"array"},"command":"evaluate","seq":5,"type":"request"}""";
            var request = JsonSerializer.Deserialize(input, DebugAdapterSerializerContext.Default.Request);
            var output = JsonSerializer.Serialize(request, DebugAdapterSerializerContext.Default.Request);

            var roundtrip = Assert.IsType<EvaluateRequest>(JsonSerializer.Deserialize(output, DebugAdapterSerializerContext.Default.Request));
            Assert.Equal("array", roundtrip.Arguments!.Expression);
            Assert.Equal(5, roundtrip.Sequence);
        }

        [Fact]
        public void OtherCommands_AreOnlyRead()
        {
            var request = new Request() { Sequence = 1, Command = "continue" };
            Assert.Throws<NotSupportedException>(() => JsonSerializer.Serialize(request, DebugAdapterSerializerContext.Default.Request));
        }
    }
}
