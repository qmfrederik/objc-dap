using Objc.Dap.Runtime;

namespace ObjcDap.Tests.Runtime
{
    public class Objc2InstanceVariableTests
    {
        [Theory]
        [InlineData("c", 'c', "char")]
        [InlineData("i", 'i', "int")]
        [InlineData("Q", 'Q', "unsigned long long")]
        [InlineData("d", 'd', "double")]
        [InlineData("B", 'B', "bool")]
        [InlineData("#", '#', "Class")]
        [InlineData("r*", '*', "char *")]
        [InlineData("@", '@', "id")]
        [InlineData("@?", '@', "block")]
        [InlineData("@\"NSString\"", '@', "NSString *")]
        [InlineData("@\"<NSCopying>\"", '@', "id<NSCopying>")]
        [InlineData("{CGPoint=dd}", '{', "{CGPoint=dd}")]
        [InlineData("^i", '^', "^i")]
        public void TypeEncoding_MapsToKindAndName(string encoding, char kind, string typeName)
        {
            var instanceVariable = new Objc2InstanceVariable() { Name = "x", TypeEncoding = encoding };

            Assert.Equal(kind, instanceVariable.EncodingKind);
            Assert.Equal(typeName, instanceVariable.TypeName);
        }
    }
}
