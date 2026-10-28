using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Objc.Dap;

namespace ObjcDap.Tests
{
    public class Objc2DebugAdapterProxyTests
    {
        private readonly ITestOutputHelper outputHelper;

        public Objc2DebugAdapterProxyTests(ITestOutputHelper outputHelper)
        {
            this.outputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));
        }

        [Fact]
        public async Task SimpleTarget_CanReadVariables()
        {
            using (var client = new DapClient<Objc2DebugAdapterProxy>(outputHelper))
            {
                var task = client.RunAsync(TestContext.Current.CancellationToken);
                var stoppedDetails = await client.LaunchProgramAndRunToBreakpointAsync(
                    program: "target",
                    file: "target.m",
                    line: 8,
                    cancellationToken: TestContext.Current.CancellationToken);

                var variables = await client.GetStackVariablesAsync(stoppedDetails.ThreadId!.Value, TestContext.Current.CancellationToken);

                var str = variables.SingleOrDefault(s => s.Name == "str");
                Assert.NotNull(str);
                Assert.Equal("NSConstantString *", str.Type);
                Assert.Equal("Hello, World!", str.Value);
                Assert.Null(str.PresentationHint);

                var dict = variables.SingleOrDefault(s => s.Name == "dict");
                Assert.NotNull(dict);
                Assert.Equal("GSDictionary *", dict.Type);
                Assert.StartsWith("<GSDictionary: 0x", dict.Value);
                Assert.Null(dict.PresentationHint);

                var array = variables.SingleOrDefault(s => s.Name == "array");
                Assert.NotNull(array);
                Assert.Equal("GSInlineArray *", array.Type);
                Assert.Equal("(item1, item2)", array.Value);
                Assert.Null(array.PresentationHint);

                client.Host.SendRequestSync(new DisconnectRequest());

                await task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
            }
        }
    }
}
