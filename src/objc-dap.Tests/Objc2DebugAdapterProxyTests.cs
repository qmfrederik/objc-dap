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
                Assert.StartsWith("0x", dict.Value);
                Assert.Null(dict.PresentationHint);

                var array = variables.SingleOrDefault(s => s.Name == "array");
                Assert.NotNull(array);
                Assert.Equal("GSInlineArray *", array.Type);
                Assert.StartsWith("0x", array.Value);
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
    }
}
