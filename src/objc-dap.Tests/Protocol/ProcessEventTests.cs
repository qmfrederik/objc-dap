using Objc.Dap.Protocol;
using System.Text.Json;

namespace ObjcDap.Tests.Protocol
{
    public class ProcessEventTests
    {
        [Fact]
        public void ProcessEvent_CanReadProcessId()
        {
            var input = File.ReadAllText("Protocol/ProcessEvent.json");
            var @event = JsonSerializer.Deserialize(input, DebugAdapterSerializerContext.Default.Event);
            var processEvent = Assert.IsType<ProcessEvent>(@event);

            Assert.Equal(0x000035b0, processEvent.Body.SystemProcessId);
        }
    }
}
