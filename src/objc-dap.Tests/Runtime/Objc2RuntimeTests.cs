using Microsoft.Extensions.Logging.Abstractions;
using Objc.Dap;
using Objc.Dap.Runtime;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;

namespace ObjcDap.Tests.Runtime
{
    public class Objc2RuntimeTests
    {
        private readonly ITestOutputHelper outputHelper;

        public Objc2RuntimeTests(ITestOutputHelper outputHelper)
        {
            this.outputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));
        }

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

                if (objcValue != Objc2Object.Null && !objcValue.IsSmallObject)
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

            // Strings describe themselves.
            var descriptions = new Dictionary<string, string?>(values);

            foreach ((var runtime, var name, var address) in this.ReadLines(nameof(ObjcRuntime_CanReadStrings)))
            {
                string? className = null;

                var objcValue = runtime.ReadObject((nuint)address);

                if (objcValue != Objc2Object.Null && !objcValue.IsSmallObject)
                {
                    var objcClass = runtime.ReadClass(objcValue.Isa);
                    className = runtime.GetClassName(objcClass);
                }

                var value = Assert.IsType<string>(runtime.GetObjectValue(objcValue));
                Assert.Equal(values[name], value);
                values.Remove(name);

                Assert.Equal(descriptions[name], runtime.GetObjectDescription(objcValue));

                Assert.Equal(classNames[name], className);
                classNames.Remove(name);
            }

            // All expected values have been processed.
            Assert.Empty(classNames);
            Assert.Empty(values);
        }

        [Fact]
        public void ObjcRuntime_CanReadNumbers()
        {
            Dictionary<string, string?> classNames = new Dictionary<string, string?>()
            {
                { "smallInt", null },
                { "smallFloat", null },
                { "smallRepeatingDouble", null },
                { "smallExtendedDouble", null },
                { "boolNumber", "NSBoolNumber" },
                { "reusedInt", "NSIntNumber" },
                { "intNumber", "NSIntNumber" },
                { "longLongNumber", "NSLongLongNumber" },
                { "unsignedLongLongNumber", "NSUnsignedLongLongNumber" },
                { "doubleNumber", "NSDoubleNumber" },
            };

            Dictionary<string, object?> values = new Dictionary<string, object?>()
            {
                { "smallInt", 42 },
                { "smallFloat", 3.14f },
                { "smallRepeatingDouble", 1.0d },
                { "smallExtendedDouble", 3.14d },
                { "boolNumber", true },
                { "reusedInt", 5 },
                { "intNumber", 1234567890 },
                { "longLongNumber", 5000000000L },
                { "unsignedLongLongNumber", 18446744073709551615ul },
                { "doubleNumber", 2.718281828459045d },
            };

            Dictionary<string, string> descriptions = new Dictionary<string, string>()
            {
                { "smallInt", "42" },
                { "smallFloat", "3.14" },
                { "smallRepeatingDouble", "1" },
                { "smallExtendedDouble", "3.14" },
                { "boolNumber", "1" },
                { "reusedInt", "5" },
                { "intNumber", "1234567890" },
                { "longLongNumber", "5000000000" },
                { "unsignedLongLongNumber", "18446744073709551615" },
                { "doubleNumber", "2.718281828459045" },
            };

            foreach ((var runtime, var name, var address) in this.ReadLines(nameof(ObjcRuntime_CanReadNumbers)))
            {
                string? className = null;

                var objcValue = runtime.ReadObject((nuint)address);

                if (objcValue != Objc2Object.Null && !objcValue.IsSmallObject)
                {
                    var objcClass = runtime.ReadClass(objcValue.Isa);
                    className = runtime.GetClassName(objcClass);
                }

                Assert.Equal(classNames[name], className);
                classNames.Remove(name);

                var expectedValue = values[name]!;
                var value = runtime.GetObjectValue(objcValue);
                Assert.Equal(expectedValue, value);
                Assert.Equal(descriptions[name], runtime.GetObjectDescription(objcValue));

                values.Remove(name);
            }

            // All expected values have been processed.
            Assert.Empty(classNames);
            Assert.Empty(values);
        }

        [Fact]
        public void ObjcRuntime_CanReadBaseTypes()
        {
            Dictionary<string, string?> classNames = new Dictionary<string, string?>()
            {
                { "date", null },
                { "url", "NSURL" },
                { "point", "GSPointValue" },
                { "size", "GSSizeValue" },
                { "rect", "GSRectValue" },
            };

            Dictionary<string, object?> values = new Dictionary<string, object?>()
            {
                { "date", new DateTimeOffset(2023, 11, 14, 22, 13, 20, TimeSpan.Zero) },
                { "url", new Uri("https://example.com/path?query=1") },
                { "point", new PointF(1.5f, 2.5f) },
                { "size", new SizeF(640f, 480f) },
                { "rect", new RectangleF(10f, 20f, 300f, 200f) },
            };

            Dictionary<string, string> descriptions = new Dictionary<string, string>()
            {
                { "date", "2023-11-14 22:13:20 +0000" },
                { "url", "https://example.com/path?query=1" },
                { "point", "{x = 1.5; y = 2.5}" },
                { "size", "{width = 640; height = 480}" },
                { "rect", "{x = 10; y = 20; width = 300; height = 200}" },
            };

            foreach ((var runtime, var name, var address) in this.ReadLines(nameof(ObjcRuntime_CanReadBaseTypes)))
            {
                string? className = null;

                var objcValue = runtime.ReadObject((nuint)address);

                if (objcValue != Objc2Object.Null && !objcValue.IsSmallObject)
                {
                    var objcClass = runtime.ReadClass(objcValue.Isa);
                    className = runtime.GetClassName(objcClass);
                }

                Assert.Equal(classNames[name], className);
                classNames.Remove(name);

                var expectedValue = values[name]!;
                var value = runtime.GetObjectValue(objcValue);
                Assert.Equal(expectedValue, value);
                Assert.Equal(descriptions[name], runtime.GetObjectDescription(objcValue));

                values.Remove(name);
            }

            // All expected values have been processed.
            Assert.Empty(classNames);
            Assert.Empty(values);
        }

        [Fact]
        public void ObjcRuntime_CanReadArray()
        {
            Dictionary<string, string?> classNames = new Dictionary<string, string?>()
            {
                { "emptyArray", "GSInlineArray" },
                { "stringArray", "GSInlineArray" },
                { "mutableArray", "GSMutableArray" },
            };

            Dictionary<string, object?[]> values = new Dictionary<string, object?[]>()
            {
                { "emptyArray", new object?[]{ } },
                { "stringArray",new object?[]{ "one", "two", "three" }},
                { "mutableArray", new object?[]{ "a", "b", "c", 42 } },
            };

            Dictionary<string, string> descriptions = new Dictionary<string, string>()
            {
                { "emptyArray", "()" },
                { "stringArray", "(one, two, three)" },
                { "mutableArray", "(a, b, c, 42)" },
            };

            foreach ((var runtime, var name, var address) in this.ReadLines(nameof(ObjcRuntime_CanReadArray)))
            {
                string? className = null;

                var objcValue = runtime.ReadObject((nuint)address);

                if (objcValue != Objc2Object.Null && !objcValue.IsSmallObject)
                {
                    var objcClass = runtime.ReadClass(objcValue.Isa);
                    className = runtime.GetClassName(objcClass);
                }

                Assert.Equal(classNames[name], className);
                classNames.Remove(name);

                var expectedValue = values[name]!;
                var count = runtime.GetIndexedVariablesCount(objcValue);
                Assert.Equal(expectedValue.Length, count);

                for (int i = 0; i < count; i++)
                {
                    var nestedObject = runtime.GetIndexedVariable(objcValue, i);
                    var nestedValue = runtime.GetObjectValue(nestedObject);

                    Assert.Equal(expectedValue[i], nestedValue);
                }

                Assert.Equal(descriptions[name], runtime.GetObjectDescription(objcValue));

                values.Remove(name);
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
