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
    }
}
