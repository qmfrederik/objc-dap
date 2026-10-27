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
            using (var client = new DapClient(this.outputHelper))
            {
                var task = client.Run(TestContext.Current.CancellationToken);

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
            using (var client = new DapClient(this.outputHelper))
            {
                var task = client.Run(TestContext.Current.CancellationToken);
                var initializeResponse = client.Host.SendRequestSync(
                    new InitializeRequest()
                    {
                        AdapterID = "objc-dap",
                        LinesStartAt1 = true,
                        ColumnsStartAt1 = true,
                    });

                // Launch the debugger.
                TaskCompletionSource<InitializedEvent> initialized =
                    new TaskCompletionSource<InitializedEvent>(
                        TaskCreationOptions.RunContinuationsAsynchronously);

                client.Host.RegisterEventType<InitializedEvent>(e =>
                {
                    initialized.SetResult(e);
                });

                client.Host.SendRequest(
                    new ObjcLaunchRequest()
                    {
                        Args = {
                            Program = TestAssets.GetTargetPath("target"),
                            StopOnEntry = false,
                        }
                    },
                    (a) => { },
                    cancellationToken: TestContext.Current.CancellationToken);

                await initialized.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

                var breakpointResponse = client.Host.SendRequestSync(
                    new SetBreakpointsRequest()
                    {
                        Source = new Source()
                        {
                            Path = TestAssets.GetSourcePath("target.m"),
                        },
                        Breakpoints = new List<SourceBreakpoint>()
                        {
                            new SourceBreakpoint() { Line = 8 },
                        }
                    });

                // Send the configuration done request and wait for the program to stop at the breakpoint.
                TaskCompletionSource<StoppedEvent> stopped =
                    new TaskCompletionSource<StoppedEvent>(
                        TaskCreationOptions.RunContinuationsAsynchronously);

                client.Host.RegisterEventType<StoppedEvent>(e =>
                {
                    stopped.SetResult(e);
                });

                client.Host.SendRequestSync(new ConfigurationDoneRequest());

                var stoppedDetails = await stopped.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

                var stackTraceResponse = client.Host.SendRequestSync(
                    new StackTraceRequest()
                    {
                        ThreadId = stoppedDetails.ThreadId!.Value,
                    });

                var scopesResponse = client.Host.SendRequestSync(
                    new ScopesRequest()
                    {
                        FrameId = stackTraceResponse.StackFrames[0].Id,
                    });

                var variablesResponse = client.Host.SendRequestSync(
                    new VariablesRequest()
                    {
                        VariablesReference = scopesResponse.Scopes[0].VariablesReference,
                    });

                var str = variablesResponse.Variables.SingleOrDefault(s => s.Name == "str");
                Assert.NotNull(str);
                Assert.Equal("NSString *", str.Type);
                Assert.Null(str.PresentationHint);

                var dict = variablesResponse.Variables.SingleOrDefault(s => s.Name == "dict");
                Assert.NotNull(dict);
                Assert.Equal("NSDictionary *", dict.Type);
                Assert.Null(dict.PresentationHint);

                var array = variablesResponse.Variables.SingleOrDefault(s => s.Name == "array");
                Assert.NotNull(array);
                Assert.Equal("NSArray *", array.Type);
                Assert.Null(array.PresentationHint);

                client.Host.SendRequestSync(new DisconnectRequest());

                await task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
            }
        }
    }
}
