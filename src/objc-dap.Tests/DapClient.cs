using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Nerdbank.Streams;
using Objc.Dap;
using Objc.Dap.Runtime;
using System.Diagnostics;
using System.Text;
using Variable = Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages.Variable;
namespace ObjcDap.Tests
{
    internal class DapClient<T> : IDisposable
        where T: DebugAdapterProxy
    {
        private readonly ITestOutputHelper outputHelper;
        private readonly MonitoringStream clientStream;
        private readonly Stream serverStream;
        private readonly MonitoringStream errorStream;
        private readonly DebugProtocolHost host;
        private readonly ServiceProvider provider;

        public DapClient(ITestOutputHelper testOutputHelper)
        {
            this.outputHelper = testOutputHelper;

            (var rawClientStream, this.serverStream) = FullDuplexStream.CreatePair();
            this.clientStream = new MonitoringStream(rawClientStream);
            this.errorStream = new MonitoringStream(Stream.Null);

            this.clientStream.DidReadAny += (sender, e) =>
            {
                Debug.WriteLine("[DAP Input] " + Encoding.UTF8.GetString(e));
                this.outputHelper.WriteLine("[DAP Input] " + Encoding.UTF8.GetString(e));
            };

            this.clientStream.DidWriteAny += (sender, e) =>
            {
                Debug.WriteLine("[DAP Output] " + Encoding.UTF8.GetString(e));
                this.outputHelper.WriteLine("[DAP Output] " + Encoding.UTF8.GetString(e));
            };

            this.errorStream.DidWriteAny += (sender, e) =>
            {
                Debug.WriteLine("[DAP Error] " + Encoding.UTF8.GetString(e));
                this.outputHelper.WriteLine("[DAP Error] " + Encoding.UTF8.GetString(e));
            };

            this.host = new DebugProtocolHost(this.clientStream, this.clientStream, registerStandardHandlers: false);

            var services = new ServiceCollection();
            services.AddLogging(s => s.AddXUnit(this.outputHelper));
            services.AddSingleton<DebugAdapterProxy, T>();
            services.AddSingleton<Objc2Runtime>();
            services.AddSingleton<VariableReferenceStore>();
            services.AddSingleton(ProcessMemoryReader.Create());
            services.AddSingleton(new DebugAdapterOptions()
            {
                Input = this.serverStream,
                Output = this.serverStream,
                Error = this.errorStream,
                LldbDapPath = Program.DefaultLldbDapPath()!,
                Python3Library = Program.DefaultPython3RuntimePath(),
            });

            this.provider = services.BuildServiceProvider();

            this.Proxy = this.provider.GetRequiredService<DebugAdapterProxy>();
        }

        public DebugAdapterProxy Proxy { get; private set; }
        public DebugProtocolHost Host => this.host;

        public Task RunAsync(CancellationToken cancellationToken)
        {
            var task =
                this.Proxy.RunAsync(cancellationToken)
                .ContinueWith(_ =>
                {
                    this.clientStream.Dispose();
                    host.Stop();
                },
                cancellationToken);
            host.Run();

            return task;
        }

        public Task<TEvent> WaitForEventAsync<TEvent>()
            where TEvent : DebugEvent, new()
        {
            TaskCompletionSource<TEvent> eventReceived =
                    new TaskCompletionSource<TEvent>(
                        TaskCreationOptions.RunContinuationsAsynchronously);

            this.Host.RegisterEventType<TEvent>(e =>
            {
                eventReceived.SetResult(e);
            }); 

            return eventReceived.Task;
        }

        public async Task<StoppedEvent> LaunchProgramAndRunToBreakpointAsync(string program, string file, int line, CancellationToken cancellationToken)
        {
            var initializeResponse = this.Host.SendRequestSync(
                new InitializeRequest()
                {
                    AdapterID = "objc-dap",
                    LinesStartAt1 = true,
                    ColumnsStartAt1 = true,
                });

            // Launch the debugger.
            var initialized = this.WaitForEventAsync<InitializedEvent>();

            this.Host.SendRequest(
                new ObjcLaunchRequest()
                {
                    Args = {
                        Program = TestAssets.GetTargetPath(program),
                        StopOnEntry = false,
                    }
                },
                (a) => { },
                cancellationToken: TestContext.Current.CancellationToken);

            await initialized.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            var breakpointResponse = this.Host.SendRequestSync(
                new SetBreakpointsRequest()
                {
                    Source = new Source()
                    {
                        Path = TestAssets.GetSourcePath(file),
                    },
                    Breakpoints = new List<SourceBreakpoint>()
                    {
                            new SourceBreakpoint() { Line = line },
                    }
                });

            // Send the configuration done request and wait for the program to stop at the breakpoint.
            var stopped = this.WaitForEventAsync<StoppedEvent>();

            this.Host.SendRequestSync(new ConfigurationDoneRequest());

            var stoppedDetails = await stopped.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            return stoppedDetails;
        }

        public async Task<List<Variable>> GetStackVariablesAsync(int threadId, CancellationToken cancellationToken)
        {
            var stackTraceResponse = this.Host.SendRequestSync(
                new StackTraceRequest()
                {
                    ThreadId = threadId,
                });

            var scopesResponse = this.Host.SendRequestSync(
                new ScopesRequest()
                {
                    FrameId = stackTraceResponse.StackFrames[0].Id,
                });

            var variablesResponse = this.Host.SendRequestSync(
                new VariablesRequest()
                {
                    VariablesReference = scopesResponse.Scopes[0].VariablesReference,
                });

            return variablesResponse.Variables;
        }

        public void Dispose()
        {
            this.errorStream.Dispose();
            this.serverStream.Dispose();
            this.clientStream.Dispose();
            this.provider.Dispose();
        }
    }
}
