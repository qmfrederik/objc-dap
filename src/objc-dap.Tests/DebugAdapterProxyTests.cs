using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Objc.Dap;
using System.ComponentModel;

namespace ObjcDap.Tests
{
    public class DebugAdapterProxyTests
    {
        private readonly ITestOutputHelper outputHelper;

        public DebugAdapterProxyTests(ITestOutputHelper outputHelper)
        {
            this.outputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));
        }

        [Fact]
        public async Task Connect_InvalidPath_Fails()
        {
            var services = new ServiceCollection();
            services.AddLogging(s => s.AddXUnit(this.outputHelper));
            services.AddSingleton<DebugAdapterProxy>();
            services.AddSingleton(new DebugAdapterOptions()
            {
                Input = Stream.Null,
                Output = Stream.Null,
                Error = Stream.Null,
                LldbDapPath = new FileInfo("C:\\InvalidPath\\invalid-executable.exe"),
                Python3Library = Program.TryFindPyton3Library(),
            });

            using var provider = services.BuildServiceProvider();
            var proxy = provider.GetRequiredService<DebugAdapterProxy>();

            await Assert.ThrowsAsync<Win32Exception>(() => proxy.RunAsync(TestContext.Current.CancellationToken));
        }

        /// <summary>
        /// Tests a simple start/stop loop of the debug adapter.  Requires LLDB.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task ConnectAndDisconnect_Works()
        {
            using (var client = new DapClient<DebugAdapterProxy>(this.outputHelper))
            {
                var task = client.RunAsync(TestContext.Current.CancellationToken);

                var initializeResponse = client.Host.SendRequestSync(
                    new InitializeRequest()
                    {
                        AdapterID = "objc-dap",
                        LinesStartAt1 = true,
                        ColumnsStartAt1 = true,
                    });

                client.Host.SendRequestSync(
                    new DisconnectRequest()
                    {
                    });

                await task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
            }
        }

        [Fact]
        public async Task SimpleTarget_CanReadVariables()
        {
            using (var client = new DapClient<DebugAdapterProxy>(this.outputHelper))
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
                Assert.Equal("NSString *", str.Type);
                Assert.Null(str.PresentationHint);

                var dict = variables.SingleOrDefault(s => s.Name == "dict");
                Assert.NotNull(dict);
                Assert.Equal("NSDictionary *", dict.Type);
                Assert.Null(dict.PresentationHint);

                var array = variables.SingleOrDefault(s => s.Name == "array");
                Assert.NotNull(array);
                Assert.Equal("NSArray *", array.Type);
                Assert.Null(array.PresentationHint);

                client.Host.SendRequestSync(new DisconnectRequest());

                await task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
            }
        }

        [Fact]
        public async Task SimpleTarget_CanReadInstanceVariables()
        {
            using (var client = new DapClient<DebugAdapterProxy>(this.outputHelper))
            {
                var task = client.RunAsync(TestContext.Current.CancellationToken);
                var stoppedDetails = await client.LaunchProgramAndRunToBreakpointAsync(
                    program: "SimpleTarget_CanReadInstanceVariables",
                    file: "SimpleTarget_CanReadInstanceVariables.m",
                    line: 19,
                    cancellationToken: TestContext.Current.CancellationToken);

                var variables = await client.GetStackVariablesAsync(stoppedDetails.ThreadId!.Value, TestContext.Current.CancellationToken);

                var person = variables.Single(s => s.Name == "person");
                Assert.Equal("Person *", person.Type);
                Assert.True(person.VariablesReference > 0);

                var members = client.Host.SendRequestSync(
                    new VariablesRequest()
                    {
                        VariablesReference = person.VariablesReference,
                    }).Variables;

                var instanceVariables = client.Host.SendRequestSync(
                    new VariablesRequest()
                    {
                        VariablesReference = members.Single(s => s.Name == "(anonymous)").VariablesReference,
                    }).Variables;

                var age = instanceVariables.Single(s => s.Name == "age");
                Assert.Equal("int", age.Type);

                var name = instanceVariables.Single(s => s.Name == "name");
                Assert.Equal("NSString *", name.Type);

                client.Host.SendRequestSync(new DisconnectRequest());

                await task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
            }
        }
    }
}
