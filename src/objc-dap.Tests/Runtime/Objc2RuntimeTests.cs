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
            Dictionary<string, string?> values = new Dictionary<string, string?>()
            {
                { "small_object", null },
                { "obj", "NSObject" },
                { "otherMemory", null },
                { "number", null },
                { "not_set", null },
            };

            foreach ((var runtime, var name, var address) in this.ReadLines(nameof(SimpleTarget_CanReadVariables)))
            {
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

        [Fact]
        public void ObjcRuntime_CanReadStrings()
        {
            Dictionary<string, string?> classNames = new Dictionary<string, string?>()
            {
                { "emptyString", null },
                { "tinyString", null },
                { "constantStringUTF7", "NSConstantString" },
                { "constantStringUnicode", "NSConstantString" },
                { "formattedString", "GSCInlineString" },
                { "mutableString", "GSMutableString" },
            };

            Dictionary<string, string?> values = new Dictionary<string, string?>()
            {
                { "emptyString", string.Empty },
                { "tinyString", "Hi!"},
                { "constantStringUTF7", "Hello, World!" },
                { "constantStringUnicode", "Fichier non trouvé" },
                { "formattedString", "Hello, World!" },
                { "mutableString", "You can change me" },
            };

            foreach ((var runtime, var name, var address) in this.ReadLines(nameof(ObjcRuntime_CanReadStrings)))
            {
                string? className = null;

                var objcValue = runtime.ReadObject((nuint)address);

                if (objcValue != Objc2Object.Null && !objcValue.IsTinyString)
                {
                    var objcClass = runtime.ReadClass(objcValue.Isa);
                    className = runtime.GetClassName(objcClass);
                }

                var value = runtime.GetStringValue(objcValue);
                Assert.Equal(values[name], value);
                values.Remove(name);

                Assert.Equal(classNames[name], className);
                classNames.Remove(name);
            }

            // All expected values have been processed.
            Assert.Empty(classNames);
            Assert.Empty(values);
        }

        private IEnumerable<(Objc2Runtime runtime, string variableName, ulong adddress)> ReadLines(string testName)
        {
            var psi = new ProcessStartInfo()
            {
                FileName = TestAssets.GetTargetPath(testName),
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

                yield return (runtime, name, address);
            }
        }
    }
}
