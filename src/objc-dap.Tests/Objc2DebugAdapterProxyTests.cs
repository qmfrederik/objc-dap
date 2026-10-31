using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Objc.Dap;

namespace ObjcDap.Tests
{
    public class Objc2DebugAdapterProxyTests
    {
        private readonly ITestOutputHelper outputHelper;

        public Objc2DebugAdapterProxyTests(ITestOutputHelper outputHelper)
        {
            this.outputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));
        }

        [Fact]
        public async Task SimpleTarget_CanReadVariables()
        {
            using (var client = new DapClient<Objc2DebugAdapterProxy>(outputHelper))
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
                Assert.Equal("NSConstantString *", str.Type);
                Assert.Equal("Hello, World!", str.Value);
                Assert.Null(str.PresentationHint);

                var dict = variables.SingleOrDefault(s => s.Name == "dict");
                Assert.NotNull(dict);
                Assert.Equal("GSDictionary *", dict.Type);
                Assert.Equal("GSDictionary <1 item>", dict.Value);
                Assert.Null(dict.PresentationHint);

                var array = variables.SingleOrDefault(s => s.Name == "array");
                Assert.NotNull(array);
                Assert.Equal("GSInlineArray *", array.Type);
                Assert.Equal("GSInlineArray <2 items>", array.Value);
                Assert.Null(array.PresentationHint);

                client.Host.SendRequestSync(new DisconnectRequest());

                await task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
            }
        }

        [Fact]
        public async Task SimpleTarget_CanExpandDictionary()
        {
            using (var client = new DapClient<Objc2DebugAdapterProxy>(outputHelper))
            {
                var task = client.RunAsync(TestContext.Current.CancellationToken);
                var stoppedDetails = await client.LaunchProgramAndRunToBreakpointAsync(
                    program: "target",
                    file: "target.m",
                    line: 8,
                    cancellationToken: TestContext.Current.CancellationToken);

                var variables = await client.GetStackVariablesAsync(stoppedDetails.ThreadId!.Value, TestContext.Current.CancellationToken);
                var dict = variables.Single(s => s.Name == "dict");

                Assert.True(dict.VariablesReference > 0);
                Assert.Equal(1, dict.NamedVariables);
                Assert.Null(dict.IndexedVariables);

                var entries = client.Host.SendRequestSync(
                    new VariablesRequest()
                    {
                        VariablesReference = dict.VariablesReference,
                    }).Variables;

                var entry = Assert.Single(entries);
                Assert.Equal("key", entry.Name);
                Assert.Equal("NSString *", entry.Type);
                Assert.Equal("value", entry.Value);

                var indexed = client.Host.SendRequestSync(
                    new VariablesRequest()
                    {
                        VariablesReference = dict.VariablesReference,
                        Filter = VariablesArguments.FilterValue.Indexed,
                    }).Variables;

                Assert.Empty(indexed);

                client.Host.SendRequestSync(new DisconnectRequest());

                await task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
            }
        }

        [Fact]
        public async Task SimpleTarget_CanExpandInstanceVariables()
        {
            using (var client = new DapClient<Objc2DebugAdapterProxy>(outputHelper))
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
                Assert.StartsWith("<Person: 0x", person.Value);
                Assert.True(person.VariablesReference > 0);
                Assert.True(person.NamedVariables >= 2);

                var instanceVariables = client.Host.SendRequestSync(
                    new VariablesRequest()
                    {
                        VariablesReference = person.VariablesReference,
                    }).Variables;

                // lldb-dap reports these with the wrong offsets, so the values prove they were read from the class metadata.
                var age = instanceVariables.Single(s => s.Name == "age");
                Assert.Equal("int", age.Type);
                Assert.Equal("42", age.Value);

                var name = instanceVariables.Single(s => s.Name == "name");
                Assert.StartsWith("NS", name.Type);
                Assert.Equal("Douglas Adams", name.Value);

                client.Host.SendRequestSync(new DisconnectRequest());

                await task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
            }
        }

        [Fact]
        public async Task SimpleTarget_CanEvaluateObjectWithInstanceVariables()
        {
            using (var client = new DapClient<Objc2DebugAdapterProxy>(outputHelper))
            {
                var task = client.RunAsync(TestContext.Current.CancellationToken);
                var stoppedDetails = await client.LaunchProgramAndRunToBreakpointAsync(
                    program: "SimpleTarget_CanReadInstanceVariables",
                    file: "SimpleTarget_CanReadInstanceVariables.m",
                    line: 19,
                    cancellationToken: TestContext.Current.CancellationToken);

                var frameId = client.Host.SendRequestSync(new StackTraceRequest() { ThreadId = stoppedDetails.ThreadId!.Value }).StackFrames[0].Id;

                var person = client.Host.SendRequestSync(new EvaluateRequest() { Expression = "person", FrameId = frameId, Context = EvaluateArguments.ContextValue.Hover });
                Assert.Equal("Person *", person.Type);
                Assert.StartsWith("<Person: 0x", person.Result);
                Assert.True(person.VariablesReference >= VariableReferenceStore.FirstReference);

                var instanceVariables = client.Host.SendRequestSync(new VariablesRequest() { VariablesReference = person.VariablesReference }).Variables;
                Assert.Equal("42", instanceVariables.Single(v => v.Name == "age").Value);
                Assert.Equal("Douglas Adams", instanceVariables.Single(v => v.Name == "name").Value);

                client.Host.SendRequestSync(new DisconnectRequest());

                await task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
            }
        }

        [Fact]
        public async Task SimpleTarget_CanExpandConstantString()
        {
            using (var client = new DapClient<Objc2DebugAdapterProxy>(outputHelper))
            {
                var task = client.RunAsync(TestContext.Current.CancellationToken);
                var stoppedDetails = await client.LaunchProgramAndRunToBreakpointAsync(
                    program: "target",
                    file: "target.m",
                    line: 8,
                    cancellationToken: TestContext.Current.CancellationToken);

                var variables = await client.GetStackVariablesAsync(stoppedDetails.ThreadId!.Value, TestContext.Current.CancellationToken);
                var str = variables.Single(s => s.Name == "str");

                Assert.Equal("NSConstantString *", str.Type);
                Assert.True(str.VariablesReference > 0);
                Assert.Equal(6, str.NamedVariables);

                var instanceVariables = client.Host.SendRequestSync(
                    new VariablesRequest()
                    {
                        VariablesReference = str.VariablesReference,
                    }).Variables;

                Assert.Equal(["isa", "flags", "nxcslen", "size", "hash", "nxcsptr"], instanceVariables.Select(v => v.Name));

                var flags = instanceVariables.Single(v => v.Name == "flags");
                Assert.Equal("unsigned int", flags.Type);
                Assert.Equal("0", flags.Value);

                var length = instanceVariables.Single(v => v.Name == "nxcslen");
                Assert.Equal("13", length.Value);

                var size = instanceVariables.Single(v => v.Name == "size");
                Assert.Equal("13", size.Value);

                var data = instanceVariables.Single(v => v.Name == "nxcsptr");
                Assert.Equal("char *", data.Type);
                Assert.StartsWith("0x", data.Value);

                client.Host.SendRequestSync(new DisconnectRequest());

                await task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
            }
        }

        [Fact]
        public async Task SimpleTarget_CanExpandArray()
        {
            using (var client = new DapClient<Objc2DebugAdapterProxy>(outputHelper))
            {
                var task = client.RunAsync(TestContext.Current.CancellationToken);
                var stoppedDetails = await client.LaunchProgramAndRunToBreakpointAsync(
                    program: "target",
                    file: "target.m",
                    line: 8,
                    cancellationToken: TestContext.Current.CancellationToken);

                var variables = await client.GetStackVariablesAsync(stoppedDetails.ThreadId!.Value, TestContext.Current.CancellationToken);
                var array = variables.Single(s => s.Name == "array");

                Assert.True(array.VariablesReference > 0);
                Assert.Equal(2, array.IndexedVariables);

                var elements = client.Host.SendRequestSync(
                    new VariablesRequest()
                    {
                        VariablesReference = array.VariablesReference,
                    }).Variables;

                Assert.Collection(
                    elements,
                    e =>
                    {
                        Assert.Equal("[0]", e.Name);
                        Assert.Equal("NSString *", e.Type);
                        Assert.Equal("item1", e.Value);
                    },
                    e =>
                    {
                        Assert.Equal("[1]", e.Name);
                        Assert.Equal("NSString *", e.Type);
                        Assert.Equal("item2", e.Value);
                    });

                var page = client.Host.SendRequestSync(
                    new VariablesRequest()
                    {
                        VariablesReference = array.VariablesReference,
                        Start = 1,
                        Count = 1,
                    }).Variables;

                var second = Assert.Single(page);
                Assert.Equal("[1]", second.Name);

                var named = client.Host.SendRequestSync(
                    new VariablesRequest()
                    {
                        VariablesReference = array.VariablesReference,
                        Filter = VariablesArguments.FilterValue.Named,
                    }).Variables;

                Assert.Empty(named);

                client.Host.SendRequestSync(new DisconnectRequest());

                await task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
            }
        }

        [Fact]
        public async Task SimpleTarget_CanExpandSet()
        {
            using (var client = new DapClient<Objc2DebugAdapterProxy>(outputHelper))
            {
                var task = client.RunAsync(TestContext.Current.CancellationToken);
                var stoppedDetails = await client.LaunchProgramAndRunToBreakpointAsync(
                    program: "SimpleTarget_CanExpandSet",
                    file: "SimpleTarget_CanExpandSet.m",
                    line: 6,
                    cancellationToken: TestContext.Current.CancellationToken);

                var variables = await client.GetStackVariablesAsync(stoppedDetails.ThreadId!.Value, TestContext.Current.CancellationToken);
                var set = variables.Single(s => s.Name == "set");

                Assert.Equal("GSSet *", set.Type);
                Assert.True(set.VariablesReference > 0);
                Assert.Equal(3, set.IndexedVariables);
                Assert.Null(set.NamedVariables);

                var members = client.Host.SendRequestSync(
                    new VariablesRequest()
                    {
                        VariablesReference = set.VariablesReference,
                    }).Variables;

                Assert.Equal(["[0]", "[1]", "[2]"], members.Select(m => m.Name));
                Assert.All(members, m => Assert.Equal("NSString *", m.Type));

                // Set members have no defined order.
                Assert.Equivalent(new[] { "item1", "item2", "item3" }, members.Select(m => m.Value).ToArray());

                var page = client.Host.SendRequestSync(
                    new VariablesRequest()
                    {
                        VariablesReference = set.VariablesReference,
                        Start = 1,
                        Count = 1,
                    }).Variables;

                Assert.Equal("[1]", Assert.Single(page).Name);

                var named = client.Host.SendRequestSync(
                    new VariablesRequest()
                    {
                        VariablesReference = set.VariablesReference,
                        Filter = VariablesArguments.FilterValue.Named,
                    }).Variables;

                Assert.Empty(named);

                client.Host.SendRequestSync(new DisconnectRequest());

                await task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
            }
        }

        [Fact]
        public async Task SimpleTarget_CanReadData()
        {
            using (var client = new DapClient<Objc2DebugAdapterProxy>(outputHelper))
            {
                var task = client.RunAsync(TestContext.Current.CancellationToken);
                var stoppedDetails = await client.LaunchProgramAndRunToBreakpointAsync(
                    program: "SimpleTarget_CanReadData",
                    file: "SimpleTarget_CanReadData.m",
                    line: 6,
                    cancellationToken: TestContext.Current.CancellationToken);

                var variables = await client.GetStackVariablesAsync(stoppedDetails.ThreadId!.Value, TestContext.Current.CancellationToken);
                var data = variables.Single(s => s.Name == "data");

                Assert.Equal("NSDataMalloc *", data.Type);
                Assert.Equal("<4 bytes: 01020304>", data.Value);

                // The data has no children of its own, so it falls back to its instance variables.
                Assert.True(data.VariablesReference > 0);
                Assert.Null(data.IndexedVariables);

                var instanceVariables = client.Host.SendRequestSync(
                    new VariablesRequest()
                    {
                        VariablesReference = data.VariablesReference,
                    }).Variables;

                Assert.Equal(data.NamedVariables, instanceVariables.Count);

                var length = instanceVariables.Single(v => v.Name == "length");
                Assert.Equal("4", length.Value);

                var bytes = instanceVariables.Single(v => v.Name == "bytes");
                Assert.Equal("^v", bytes.Type);
                Assert.StartsWith("0x", bytes.Value);

                // The binary viewer opens at the buffer, both from the data itself and from its bytes pointer.
                Assert.Equal(bytes.Value, bytes.MemoryReference);
                Assert.Equal(bytes.Value, data.MemoryReference);

                var memory = client.Host.SendRequestSync(
                    new ReadMemoryRequest()
                    {
                        MemoryReference = data.MemoryReference,
                        Count = 4,
                    });

                Assert.Equal(Convert.ToBase64String([1, 2, 3, 4]), memory.Data);

                client.Host.SendRequestSync(new DisconnectRequest());

                await task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
            }
        }

        [Fact]
        public async Task SimpleTarget_CanReadClass()
        {
            using (var client = new DapClient<Objc2DebugAdapterProxy>(outputHelper))
            {
                var task = client.RunAsync(TestContext.Current.CancellationToken);
                var stoppedDetails = await client.LaunchProgramAndRunToBreakpointAsync(
                    program: "SimpleTarget_CanReadClass",
                    file: "SimpleTarget_CanReadClass.m",
                    line: 8,
                    cancellationToken: TestContext.Current.CancellationToken);

                var variables = await client.GetStackVariablesAsync(stoppedDetails.ThreadId!.Value, TestContext.Current.CancellationToken);

                var cls = variables.Single(s => s.Name == "cls");
                Assert.Equal("Class", cls.Type);
                Assert.Equal("NSString", cls.Value);

                // Metaclasses carry the name of their class.
                var metaClass = variables.Single(s => s.Name == "metaClass");
                Assert.Equal("Class", metaClass.Type);
                Assert.Equal("NSString", metaClass.Value);

                var nilClass = variables.Single(s => s.Name == "nilClass");
                Assert.Equal("Class", nilClass.Type);
                Assert.Equal("nil", nilClass.Value);

                client.Host.SendRequestSync(new DisconnectRequest());

                await task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
            }
        }

        [Fact]
        public async Task SimpleTarget_CanReadEmptyCollections()
        {
            using (var client = new DapClient<Objc2DebugAdapterProxy>(outputHelper))
            {
                var task = client.RunAsync(TestContext.Current.CancellationToken);
                var stoppedDetails = await client.LaunchProgramAndRunToBreakpointAsync(
                    program: "SimpleTarget_CanReadEmptyCollections",
                    file: "SimpleTarget_CanReadEmptyCollections.m",
                    line: 8,
                    cancellationToken: TestContext.Current.CancellationToken);

                var variables = await client.GetStackVariablesAsync(stoppedDetails.ThreadId!.Value, TestContext.Current.CancellationToken);

                // Empty collections are summarized, but there is nothing to expand.
                foreach ((var name, var className) in new[] { ("emptyArray", "GSInlineArray"), ("emptyDictionary", "GSDictionary"), ("emptyMutableArray", "GSMutableArray") })
                {
                    var variable = variables.Single(v => v.Name == name);
                    Assert.Equal($"{className} *", variable.Type);
                    Assert.Equal($"{className} <0 items>", variable.Value);
                    Assert.Equal(0, variable.IndexedVariables ?? 0);
                    Assert.Equal(0, variable.NamedVariables ?? 0);
                    Assert.True(variable.VariablesReference < VariableReferenceStore.FirstReference, "The proxy must not hand out a reference for an empty collection.");
                }

                client.Host.SendRequestSync(new DisconnectRequest());

                await task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
            }
        }

        private static EvaluateArguments.ContextValue ToContext(string context) => context switch
        {
            "hover" => EvaluateArguments.ContextValue.Hover,
            "watch" => EvaluateArguments.ContextValue.Watch,
            "repl" => EvaluateArguments.ContextValue.Repl,
            "clipboard" => EvaluateArguments.ContextValue.Clipboard,
            _ => throw new ArgumentOutOfRangeException(nameof(context)),
        };

        [Theory]
        [InlineData("hover")]
        [InlineData("watch")]
        public async Task SimpleTarget_CanEvaluateObjects(string context)
        {
            using (var client = new DapClient<Objc2DebugAdapterProxy>(outputHelper))
            {
                var task = client.RunAsync(TestContext.Current.CancellationToken);
                var stoppedDetails = await client.LaunchProgramAndRunToBreakpointAsync(
                    program: "target",
                    file: "target.m",
                    line: 8,
                    cancellationToken: TestContext.Current.CancellationToken);

                var frameId = client.Host.SendRequestSync(new StackTraceRequest() { ThreadId = stoppedDetails.ThreadId!.Value }).StackFrames[0].Id;

                var str = client.Host.SendRequestSync(new EvaluateRequest() { Expression = "str", FrameId = frameId, Context = ToContext(context) });
                Assert.Equal("NSConstantString *", str.Type);
                Assert.Equal("Hello, World!", str.Result);

                var array = client.Host.SendRequestSync(new EvaluateRequest() { Expression = "array", FrameId = frameId, Context = ToContext(context) });
                Assert.Equal("GSInlineArray *", array.Type);
                Assert.Equal("GSInlineArray <2 items>", array.Result);
                Assert.True(array.VariablesReference >= VariableReferenceStore.FirstReference);
                Assert.Equal(2, array.IndexedVariables);

                // The result can be expanded like any other variable.
                var elements = client.Host.SendRequestSync(new VariablesRequest() { VariablesReference = array.VariablesReference }).Variables;
                Assert.Equal(["item1", "item2"], elements.Select(e => e.Value));

                client.Host.SendRequestSync(new DisconnectRequest());

                await task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
            }
        }

        [Theory]
        [InlineData("repl")]
        [InlineData("clipboard")]
        public async Task SimpleTarget_LeavesOtherEvaluateContextsAlone(string context)
        {
            using (var client = new DapClient<Objc2DebugAdapterProxy>(outputHelper))
            {
                var task = client.RunAsync(TestContext.Current.CancellationToken);
                var stoppedDetails = await client.LaunchProgramAndRunToBreakpointAsync(
                    program: "target",
                    file: "target.m",
                    line: 8,
                    cancellationToken: TestContext.Current.CancellationToken);

                var frameId = client.Host.SendRequestSync(new StackTraceRequest() { ThreadId = stoppedDetails.ThreadId!.Value }).StackFrames[0].Id;

                var array = client.Host.SendRequestSync(new EvaluateRequest() { Expression = "array", FrameId = frameId, Context = ToContext(context) });
                Assert.Equal("NSArray *", array.Type);
                Assert.DoesNotContain("items", array.Result);

                client.Host.SendRequestSync(new DisconnectRequest());

                await task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
            }
        }
    }
}
