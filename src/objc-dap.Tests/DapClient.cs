using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Nerdbank.Streams;
using Objc.Dap;
using System.Diagnostics;
using System.Text;

namespace ObjcDap.Tests
{
    internal class DapClient : IDisposable
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

            this.host = new DebugProtocolHost(this.clientStream, this.clientStream);

            var services = new ServiceCollection();
            services.AddLogging(s => s.AddXUnit(this.outputHelper));
            services.AddSingleton<DebugAdapterProxy>();
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

        public Task Run(CancellationToken cancellationToken)
        {
            var task =
                this.Proxy.RunAsync(cancellationToken)
                .ContinueWith(_ =>
                {
                    this.clientStream.Dispose();
                    host.Stop();
                }, cancellationToken);
            host.Run();

            return task;
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
