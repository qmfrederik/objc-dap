using Microsoft.Extensions.Logging.Abstractions;
using Objc.Dap;
using Objc.Dap.Runtime;
using System.Diagnostics;
using System.Globalization;

namespace ObjcDap.Tests.Runtime
{
    public class Objc2RuntimeTests
    {
        [Fact]
        public void SimpleTarget_CanReadVariables()
        {
            var psi = new ProcessStartInfo()
            {
                FileName = DebugAdapterProxyTests.GetTargetPath(nameof(SimpleTarget_CanReadVariables)),
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
            };

            var process = Process.Start(psi);
            Assert.NotNull(process);

            var reader = ProcessMemoryReader.Create();
            reader.Open(process.Id);

            var runtime = new Objc2Runtime(
                reader,
                NullLogger<Objc2Runtime>.Instance);

            Dictionary<string, string?> values = new Dictionary<string, string?>()
            {
                { "small_object", null },
                { "obj", "NSObject" },
                { "otherMemory", null },
                { "number", null },
                { "not_set", null },
            };

            while (true)
            {
                var line = process.StandardOutput.ReadLine();

                if (line == null || line == "---")
                {
                    break;
                }

                var parts = line.Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                var name = parts[0];
                var address = ulong.Parse(parts[1].AsSpan(2), NumberStyles.HexNumber);

                string? className = null;

                var objcValue = runtime.ReadObject((nuint)address);

                if (objcValue != Objc2Object.Null)
                {
                    var objcClass = runtime.ReadClass(objcValue.Isa);
                    className = runtime.GetClassName(objcClass);
                }

                Assert.Equal(values[name], className);
                values.Remove(name);
            }

            // All expected values have been processed.
            Assert.Empty(values);
        }
    }
}
