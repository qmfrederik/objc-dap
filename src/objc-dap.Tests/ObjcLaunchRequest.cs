using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;

namespace ObjcDap.Tests
{
    internal class ObjcLaunchRequest : DebugRequest<ObjcLaunchArguments>
    {
        public ObjcLaunchRequest(): base("launch")
        {
        }
    }
}
