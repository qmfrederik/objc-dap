using Objc.Dap;
using System.Buffers;
using System.Text;

namespace ObjcDap.Tests
{
    public class MessageSummaryTests
    {
        [Theory]
        [InlineData("""{"seq":12,"type":"request","command":"variables","arguments":{"variablesReference":3}}""", "request command=variables seq=12")]
        [InlineData("""{"seq":5,"type":"response","request_seq":12,"success":true,"command":"variables","body":{"variables":[]}}""", "response command=variables seq=5 request_seq=12")]
        [InlineData("""{"seq":7,"type":"event","event":"stopped","body":{"reason":"breakpoint","threadId":1}}""", "event event=stopped seq=7")]
        // Properties of nested objects must not be mistaken for top-level ones.
        [InlineData("""{"arguments":{"command":"nested","seq":99},"seq":1,"type":"request","command":"evaluate"}""", "request command=evaluate seq=1")]
        [InlineData("""{"seq":1}""", "unknown seq=1")]
        [InlineData("{}", "unknown")]
        public void Describe_ReturnsSummary(string json, string expected)
        {
            Assert.Equal(expected, MessageSummary.Describe(ToSequence(json)));
        }

        [Fact]
        public void Describe_MalformedJson_ReturnsWhatWasParsed()
        {
            Assert.Equal("request command=next seq=3", MessageSummary.Describe(ToSequence("""{"seq":3,"type":"request","command":"next","arguments":{""")));
        }

        [Fact]
        public void Describe_NotJson_DoesNotThrow()
        {
            Assert.Equal("unknown", MessageSummary.Describe(ToSequence("not json")));
        }

        [Fact]
        public void Describe_MultipleSegments_Works()
        {
            var first = new Segment(Encoding.UTF8.GetBytes("""{"seq":1,"type":"requ"""));
            var last = first.Append(Encoding.UTF8.GetBytes("""est","command":"next"}"""));
            var sequence = new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);

            Assert.Equal("request command=next seq=1", MessageSummary.Describe(sequence));
        }

        private static ReadOnlySequence<byte> ToSequence(string json) => new(Encoding.UTF8.GetBytes(json));

        private sealed class Segment : ReadOnlySequenceSegment<byte>
        {
            public Segment(ReadOnlyMemory<byte> memory)
            {
                this.Memory = memory;
            }

            public Segment Append(ReadOnlyMemory<byte> memory)
            {
                var next = new Segment(memory) { RunningIndex = this.RunningIndex + this.Memory.Length };
                this.Next = next;
                return next;
            }
        }
    }
}
