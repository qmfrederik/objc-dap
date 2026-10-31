using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.Threading;
using Moq;
using Objc.Dap;
using Objc.Dap.Protocol;
using Objc.Dap.Runtime;
using System.Buffers;
using System.ComponentModel;
using System.Text;
using System.Text.Json;

namespace ObjcDap.Tests
{
    public class Objc2DebugAdapterProxyReferenceTests
    {
        public static TheoryData<string> InvalidatingCommands => new TheoryData<string>
        {
            "continue",
            "next",
            "stepIn",
            "stepOut",
            "stepBack",
            "reverseContinue",
            "restartFrame",
            "goto",
            "restart",
            "terminate",
            "disconnect",
        };

        public static TheoryData<string> NonInvalidatingCommands => new TheoryData<string>
        {
            "initialize",
            "launch",
            "setBreakpoints",
            "configurationDone",
            "threads",
            "stackTrace",
            "scopes",
            "variables",
            "evaluate",
            "pause",
        };

        [Theory]
        [MemberData(nameof(InvalidatingCommands))]
        public void Request_ResumingOrEndingSession_InvalidatesReferences(string command)
        {
            var harness = new Harness();
            var reference = harness.References.Add(0x1000);

            harness.Send(CreateRequest(command));

            Assert.Equal(0, harness.References.Count);
            Assert.False(harness.References.TryGetAddress(reference, out _));
        }

        [Theory]
        [MemberData(nameof(NonInvalidatingCommands))]
        public void Request_NotResuming_KeepsReferences(string command)
        {
            var harness = new Harness();
            var reference = harness.References.Add(0x1000);

            harness.Send(CreateRequest(command));

            Assert.Equal(1, harness.References.Count);
            Assert.True(harness.References.TryGetAddress(reference, out var address));
            Assert.Equal((nuint)0x1000, address);
        }

        [Fact]
        public void ProcessEvent_InvalidatesReferencesAndOpensProcess()
        {
            var harness = new Harness();
            harness.References.Add(0x1000);

            harness.Send("""{"seq":1,"type":"event","event":"process","body":{"name":"target","systemProcessId":1234}}""");

            Assert.Equal(0, harness.References.Count);
            harness.Reader.Verify(r => r.Open(1234), Times.Once);
        }

        [Fact]
        public void Request_IsForwardedUnchanged()
        {
            var harness = new Harness();
            var json = CreateRequest("next");

            harness.Send(json);

            var message = Assert.Single(harness.Forwarded);
            var expected = Encoding.UTF8.GetBytes($"Content-Length: {Encoding.UTF8.GetByteCount(json)}\r\n\r\n{json}");
            Assert.Equal(expected, message.Value.ToArray());
            Assert.Equal(ProtocolMessageType.Request, message.Type);
        }

        [Fact]
        public void VariablesRequest_UnknownReference_IsForwarded()
        {
            var harness = new Harness();

            harness.Send(CreateVariablesRequest(7, 1));

            Assert.Single(harness.Forwarded);
            Assert.Empty(harness.Responses);
        }

        [Fact]
        public void VariablesRequest_ExpiredReference_IsForwarded()
        {
            var harness = new Harness();
            var reference = harness.References.Add(0x1000);
            harness.Send(CreateRequest("next"));
            harness.Forwarded.ToList();

            harness.Send(CreateVariablesRequest(7, reference));

            Assert.Single(harness.Forwarded);
            Assert.Empty(harness.Responses);
        }

        [Fact]
        public void VariablesRequest_OwnedReferenceToUnreadableObject_FailsWithoutForwarding()
        {
            var harness = new Harness();
            var reference = harness.References.Add(0x1000);

            harness.Send(CreateVariablesRequest(7, reference));

            Assert.Empty(harness.Forwarded);
            var response = Assert.IsType<VariablesResponse>(Assert.Single(harness.Responses).Payload);
            Assert.False(response.Success);
            Assert.Equal(7, response.RequestSequence);
            Assert.NotNull(response.Message);
        }

        [Fact]
        public void ProcessEvent_WithoutProcessId_IsForwarded()
        {
            var harness = new Harness();

            harness.Send("""{"seq":1,"type":"event","event":"process","body":{"name":"target"}}""");

            Assert.Single(harness.Forwarded);
            harness.Reader.Verify(r => r.Open(It.IsAny<int>()), Times.Never);
        }

        [Theory]
        [InlineData(typeof(InvalidOperationException))]
        [InlineData(typeof(Win32Exception))]
        [InlineData(typeof(NotSupportedException))]
        public void ProcessEvent_ProcessCanNotBeOpened_IsForwarded(Type exceptionType)
        {
            var harness = new Harness();
            harness.Reader.Setup(r => r.Open(It.IsAny<int>())).Throws((Exception)Activator.CreateInstance(exceptionType)!);

            harness.Send("""{"seq":1,"type":"event","event":"process","body":{"name":"target","systemProcessId":1234}}""");

            Assert.Single(harness.Forwarded);
        }

        [Fact]
        public void VariablesResponse_UnreadableObject_KeepsTheReportedValue()
        {
            var harness = new Harness(new UnreadableMemoryReader());

            harness.Send(CreateVariablesResponse("NSString *", "0x0000000000001000"));

            var response = Assert.IsType<VariablesResponse>(Assert.Single(harness.Forwarded).Payload);
            var variable = Assert.Single(response.Body.Variables);
            Assert.Equal("NSString *", variable.Type);
            Assert.Equal("0x0000000000001000", variable.Value);
        }

        [Fact]
        public void Message_ThatCanNotBeProcessed_IsForwardedUnchanged()
        {
            var harness = new Harness();

            // The process event has to open the process, and does not expect this exception.
            harness.Reader.Setup(r => r.Open(It.IsAny<int>())).Throws(new NotSupportedException());
            var json = """{"seq":1,"type":"event","event":"process","body":{"name":"target","systemProcessId":1234}}""";

            harness.Send(json);

            var message = Assert.Single(harness.Forwarded);
            var expected = Encoding.UTF8.GetBytes($"Content-Length: {Encoding.UTF8.GetByteCount(json)}\r\n\r\n{json}");
            Assert.Equal(expected, message.Value.ToArray());
        }

        [Fact]
        public void Request_DeserializesCommand()
        {
            var request = JsonSerializer.Deserialize(
                CreateRequest("stepIn"),
                DebugAdapterSerializerContext.Default.Request);

            Assert.NotNull(request);
            Assert.Equal("stepIn", request.Command);
        }

        private static string CreateRequest(string command) =>
            "{\"seq\":1,\"type\":\"request\",\"command\":\"" + command + "\",\"arguments\":{}}";

        private static string CreateVariablesRequest(int seq, int reference) =>
            "{\"seq\":" + seq + ",\"type\":\"request\",\"command\":\"variables\",\"arguments\":{\"variablesReference\":" + reference + "}}";

        private static string CreateVariablesResponse(string type, string value) =>
            "{\"seq\":9,\"type\":\"response\",\"request_seq\":7,\"success\":true,\"command\":\"variables\",\"body\":{\"variables\":[" +
            "{\"name\":\"s\",\"type\":\"" + type + "\",\"value\":\"" + value + "\",\"variablesReference\":3}]}}";

        // A process of which no memory can be read, as if every pointer was invalid.
        private sealed class UnreadableMemoryReader : ProcessMemoryReader
        {
            public override void Open(int processId)
            {
            }

            public override int Read(nuint baseAddress, Span<byte> buffer) => throw new Win32Exception(299);

            public override bool IsDereferencableMemory(nuint baseAddress) => true;

            public override void Dispose()
            {
            }
        }

        private sealed class Harness
        {
            private readonly TestProxy proxy;
            private readonly AsyncQueue<Message?> target = new AsyncQueue<Message?>();

            public Harness()
                : this(null)
            {
            }

            // Uses a reader other than the mocked one, e.g. one which can't read the debuggee.
            public Harness(ProcessMemoryReader? reader)
            {
                this.Reader = new Mock<ProcessMemoryReader>();
                this.References = new VariableReferenceStore();

                var options = new DebugAdapterOptions()
                {
                    LldbDapPath = new FileInfo("lldb-dap"),
                    Input = Stream.Null,
                    Output = Stream.Null,
                    Error = Stream.Null,
                };

                this.proxy = new TestProxy(
                    options,
                    new Objc2Runtime(reader ?? this.Reader.Object, NullLogger<Objc2Runtime>.Instance),
                    this.References);
            }

            public Mock<ProcessMemoryReader> Reader { get; }

            public VariableReferenceStore References { get; }

            public IReadOnlyList<Message> Forwarded => Drain(this.target);

            public IReadOnlyList<Message> Responses => Drain(this.proxy.Client);

            private static List<Message> Drain(AsyncQueue<Message?> queue)
            {
                var messages = new List<Message>();
                while (queue.TryDequeue(out var message))
                {
                    messages.Add(message!);
                }

                return messages;
            }

            public void Send(string json)
            {
                var message = Encoding.UTF8.GetBytes(json);
                var withHeader = Encoding.UTF8.GetBytes($"Content-Length: {message.Length}\r\n\r\n{json}");

                var protocolMessage = JsonSerializer.Deserialize(json, DebugAdapterSerializerContext.Default.ProtocolMessage)!;
                protocolMessage = protocolMessage.Type switch
                {
                    ProtocolMessageType.Request => JsonSerializer.Deserialize(json, DebugAdapterSerializerContext.Default.Request)!,
                    ProtocolMessageType.Event => JsonSerializer.Deserialize(json, DebugAdapterSerializerContext.Default.Event)!,
                    ProtocolMessageType.Response => JsonSerializer.Deserialize(json, DebugAdapterSerializerContext.Default.Response)!,
                    _ => protocolMessage,
                };

                this.proxy.Send(new ReadOnlySequence<byte>(withHeader), new ReadOnlySequence<byte>(message), protocolMessage, this.target);
            }
        }

        private sealed class TestProxy : Objc2DebugAdapterProxy
        {
            public TestProxy(DebugAdapterOptions options, Objc2Runtime runtime, VariableReferenceStore references)
                : base(options, runtime, references, NullLogger<DebugAdapterProxy>.Instance)
            {
            }

            public AsyncQueue<Message?> Client => this.ClientQueue;

            public void Send(ReadOnlySequence<byte> messageWithHeader, ReadOnlySequence<byte> messageBytes, ProtocolMessage message, AsyncQueue<Message?> target) =>
                this.ProxyMessage(messageWithHeader, messageBytes, message, target);
        }
    }
}
