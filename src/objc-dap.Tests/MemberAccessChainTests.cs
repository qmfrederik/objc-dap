using Objc.Dap;

namespace ObjcDap.Tests
{
    public class MemberAccessChainTests
    {
        [Theory]
        [InlineData("a->b", "a", new[] { "b" })]
        [InlineData("person->name", "person", new[] { "name" })]
        [InlineData("o1->o2->name", "o1", new[] { "o2", "name" })]
        [InlineData("a->b->c->d", "a", new[] { "b", "c", "d" })]
        [InlineData("  a  ->  b  ", "a", new[] { "b" })]
        [InlineData("_a1->_b2", "_a1", new[] { "_b2" })]
        [InlineData("self->_ivar", "self", new[] { "_ivar" })]
        public void Parses_MemberAccessChains(string text, string root, string[] members)
        {
            Assert.True(MemberAccessChain.TryParse(text, out var chain));
            Assert.Equal(root, chain.Root);
            Assert.Equal(members, chain.Members);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("a")]
        [InlineData("a->")]
        [InlineData("->b")]
        [InlineData("a->->b")]
        [InlineData("a-> ->b")]
        [InlineData("a->b->")]
        [InlineData("a- >b")]
        [InlineData("a->b.c")]
        [InlineData("a.b")]
        [InlineData("a->b()")]
        [InlineData("a->b[0]")]
        [InlineData("a[0]->b")]
        [InlineData("(a)->b")]
        [InlineData("(a->b)")]
        [InlineData("*a->b")]
        [InlineData("&a->b")]
        [InlineData("a->b + 1")]
        [InlineData("a->b = 1")]
        [InlineData("a->b;")]
        [InlineData("[a b]->c")]
        [InlineData("a->1b")]
        [InlineData("1a->b")]
        [InlineData("a->b c")]
        public void Rejects_EverythingElse(string? text)
        {
            Assert.False(MemberAccessChain.TryParse(text, out var chain));
            Assert.Null(chain);
        }
    }
}
