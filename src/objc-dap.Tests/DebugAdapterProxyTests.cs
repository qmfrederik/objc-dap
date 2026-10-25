using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Nerdbank.Streams;
using Objc.Dap;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;

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
            (var clientStream, var serverStream) = FullDuplexStream.CreatePair();
            var monitoringClientStream = new MonitoringStream(clientStream);
            var monitoringErrorStream = new MonitoringStream(Stream.Null);

            monitoringClientStream.DidReadAny += (sender, e) =>
            {
                Debug.WriteLine("[DAP Input] " + Encoding.UTF8.GetString(e));
                this.outputHelper.WriteLine("[DAP Input] " + Encoding.UTF8.GetString(e));
            };

            monitoringClientStream.DidWriteAny += (sender, e) =>
            {
                Debug.WriteLine("[DAP Output] " + Encoding.UTF8.GetString(e));
                this.outputHelper.WriteLine("[DAP Output] " + Encoding.UTF8.GetString(e));
            };

            monitoringErrorStream.DidWriteAny += (sender, e) =>
            {
                Debug.WriteLine("[DAP Error] " + Encoding.UTF8.GetString(e));
                this.outputHelper.WriteLine("[DAP Error] " + Encoding.UTF8.GetString(e));
            };

            DebugProtocolHost host = new DebugProtocolHost(monitoringClientStream, monitoringClientStream);

            var services = new ServiceCollection();
            services.AddLogging(s => s.AddXUnit(this.outputHelper));
            services.AddSingleton<DebugAdapterProxy>();
            services.AddSingleton(new DebugAdapterOptions()
            {
                Input = serverStream,
                Output = serverStream,
                Error = monitoringErrorStream,
                LldbDapPath = Program.DefaultLldbDapPath()!,
                Python3Library = Program.DefaultPython3RuntimePath(),
            });

            using var provider = services.BuildServiceProvider();
            var proxy = provider.GetRequiredService<DebugAdapterProxy>();

            var task =
                proxy.RunAsync(TestContext.Current.CancellationToken)
                .ContinueWith(_ =>
                {
                    monitoringClientStream.Dispose();
                    host.Stop();
                }, TestContext.Current.CancellationToken);
            host.Run();

            var initializeResponse = host.SendRequestSync(
                new InitializeRequest()
                {
                    AdapterID = "objc-dap",
                    LinesStartAt1 = true,
                    ColumnsStartAt1 = true,
                });

            host.SendRequestSync(
                new DisconnectRequest()
                {
                });

            await task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        }
    }
}
