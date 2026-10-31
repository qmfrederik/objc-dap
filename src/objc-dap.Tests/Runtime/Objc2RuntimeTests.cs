using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Objc.Dap;
using Objc.Dap.Runtime;
using System.Diagnostics;
using System.Drawing;
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

                values.Remove(name);
            }

            // All expected values have been processed.
            Assert.Empty(classNames);
            Assert.Empty(values);
        }

        [Fact]
        public void ObjcRuntime_CanReadDictionary()
        {
            Dictionary<string, string?> classNames = new Dictionary<string, string?>()
            {
                { "emptyDictionary", "GSDictionary" },
                { "stringDictionary", "GSDictionary" },
                { "mutableDictionary", "GSMutableDictionary" },
            };

            Dictionary<string, Dictionary<string, object?>> values = new Dictionary<string, Dictionary<string, object?>>()
            {
                { "emptyDictionary", new Dictionary<string, object?>() },
                { "stringDictionary", new Dictionary<string, object?>() { { "one", "uno" }, { "two", "dos" }, { "three", "tres" } } },
                { "mutableDictionary", Enumerable.Range(0, 20).ToDictionary(i => $"key{i}", i => (object?)(i * 10)) },
            };

            foreach ((var runtime, var name, var address) in this.ReadLines(nameof(ObjcRuntime_CanReadDictionary)))
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

                var expectedValue = values[name];
                var count = runtime.GetNamedVariablesCount(objcValue);
                Assert.Equal(expectedValue.Count, count);

                for (int i = 0; i < count; i++)
                {
                    (var key, var nestedObject) = runtime.GetNamedVariable(objcValue, i);

                    Assert.Contains(key, expectedValue.Keys);
                    Assert.Equal(expectedValue[key], runtime.GetObjectValue(nestedObject));
                    expectedValue.Remove(key);
                }

                Assert.Empty(expectedValue);
                values.Remove(name);
            }

            // All expected values have been processed.
            Assert.Empty(classNames);
            Assert.Empty(values);
        }

        [Fact]
        public void ObjcRuntime_CanReadSet()
        {
            Dictionary<string, string?> classNames = new Dictionary<string, string?>()
            {
                { "emptySet", "GSSet" },
                { "stringSet", "GSSet" },
                { "mutableSet", "GSMutableSet" },
            };

            Dictionary<string, List<object?>> values = new Dictionary<string, List<object?>>()
            {
                { "emptySet", new List<object?>() },
                { "stringSet", new List<object?>() { "one", "two", "three" } },
                { "mutableSet", Enumerable.Range(0, 20).Select(i => (object?)(i * 10)).ToList() },
            };

            foreach ((var runtime, var name, var address) in this.ReadLines(nameof(ObjcRuntime_CanReadSet)))
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

                // Set members have no defined order.
                var expectedValue = values[name];
                var count = runtime.GetIndexedVariablesCount(objcValue);
                Assert.Equal(expectedValue.Count, count);

                for (int i = 0; i < count; i++)
                {
                    var nestedObject = runtime.GetIndexedVariable(objcValue, i);
                    Assert.True(expectedValue.Remove(runtime.GetObjectValue(nestedObject)));
                }

                Assert.Empty(expectedValue);
                Assert.Equal(-1, runtime.GetNamedVariablesCount(objcValue));
                values.Remove(name);
            }

            // All expected values have been processed.
            Assert.Empty(classNames);
            Assert.Empty(values);
        }

        [Fact]
        public void ObjcRuntime_NonDictionary_HasNoNamedVariables()
        {
            foreach ((var runtime, var name, var address) in this.ReadLines(nameof(ObjcRuntime_CanReadArray)))
            {
                var objcValue = runtime.ReadObject((nuint)address);
                Assert.Equal(-1, runtime.GetNamedVariablesCount(objcValue));
            }
        }

        [Fact]
        public void ObjcRuntime_CanReadInstanceVariables()
        {
            // Offsets come from the runtime's own ivar_getOffset in the target.
            Dictionary<string, (string Encoding, uint Size)> expected = new Dictionary<string, (string Encoding, uint Size)>()
            {
                { "baseInt", ("i", 4) },
                { "derivedDouble", ("d", 8) },
                { "derivedString", ("@", 8) },
                { "derivedChar", ("c", 1) },
            };

            IReadOnlyList<Objc2InstanceVariable>? instanceVariables = null;

            foreach ((var runtime, var name, var address) in this.ReadLines(nameof(ObjcRuntime_CanReadInstanceVariables)))
            {
                if (name == "derived")
                {
                    instanceVariables = runtime.GetInstanceVariables(runtime.ReadObject((nuint)address));
                    continue;
                }

                Assert.NotNull(instanceVariables);
                var instanceVariable = Assert.Single(instanceVariables, v => v.Name == name);

                Assert.Equal((int)address, instanceVariable.Offset);
                Assert.StartsWith(expected[name].Encoding, instanceVariable.TypeEncoding);
                Assert.Equal(expected[name].Size, instanceVariable.Size);
                expected.Remove(name);
            }

            // All expected values have been processed.
            Assert.Empty(expected);

            // Superclass instance variables come first.
            var names = instanceVariables!.Select(v => v.Name).ToList();
            Assert.True(names.IndexOf("baseInt") < names.IndexOf("derivedDouble"));
        }

        [Fact]
        public void ObjcRuntime_CanReadInstanceVariableValues()
        {
            Dictionary<string, object?> expected = new Dictionary<string, object?>()
            {
                { "baseInt", 42 },
                { "derivedDouble", 3.5 },
                { "derivedString", "text" },
                { "derivedChar", (sbyte)'x' },
            };

            foreach ((var runtime, var name, var address) in this.ReadLines(nameof(ObjcRuntime_CanReadInstanceVariables)))
            {
                if (name != "derived")
                {
                    continue;
                }

                var derived = runtime.ReadObject((nuint)address);

                foreach (var instanceVariable in runtime.GetInstanceVariables(derived))
                {
                    if (!expected.Remove(instanceVariable.Name, out var expectedValue))
                    {
                        continue;
                    }

                    var value = runtime.GetInstanceVariableValue(derived, instanceVariable);

                    if (value is Objc2Object child)
                    {
                        value = runtime.GetObjectValue(child);
                    }

                    Assert.Equal(expectedValue, value);
                }
            }

            // All expected values have been processed.
            Assert.Empty(expected);
        }

        [Fact]
        public void ObjcRuntime_NullAndSmallObjects_HaveNoInstanceVariables()
        {
            var runtime = new Objc2Runtime(
                Mock.Of<ProcessMemoryReader>(),
                NullLogger<Objc2Runtime>.Instance);

            Assert.Empty(runtime.GetInstanceVariables(Objc2Object.Null));
            Assert.Empty(runtime.GetInstanceVariables(new Objc2Object() { Address = 0x3 }));
            Assert.Throws<ArgumentNullException>(() => runtime.GetInstanceVariables(null!));
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
