using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Newtonsoft.Json;

namespace ObjcDap.Tests
{
    // https://lldb.llvm.org/use/lldbdap.html#launch-configurations
    internal class ObjcLaunchArguments : LaunchArguments
    {
        /// <summary>
        /// Gets or sets the path to the executable to launch.
        /// </summary>
        [JsonProperty("program")]
        public string? Program { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to stop program immediately after launching.
        /// </summary>
        [JsonProperty("stopOnEntry")]
        public bool StopOnEntry { get; set; } = false;
    }
}
