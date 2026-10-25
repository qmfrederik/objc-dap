using System.Text.Json.Serialization;

namespace Objc.Dap.Protocol
{
    public class ProcessEventBody
    {
        /// <summary>
        /// Gets or sets the logical name of the process. This is usually the full path to
        /// process's executable file. Example: /home/example/myproj/program.js.
        /// </summary>
        [JsonPropertyName("name")]
        public required string Name { get; set; }

        /// <summary>
        /// Gets or sets the process ID of the debugged process, as assigned by the operating
        /// system. This property should be omitted for logical processes that do not
        /// map to operating system processes on the machine.
        /// </summary>
        [JsonPropertyName("systemProcessId")]
        public int? SystemProcessId { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the process is running on the same
        /// computer as the debug adapter.
        /// </summary>
        [JsonPropertyName("isLocalProcess")]
        public bool? IsLocalProcess { get; set; }

        /// <summary>
        /// Gets or sets how the debug engine started debugging this process.
        /// Values:
        /// 'launch': Process was launched under the debugger.
        /// 'attach': Debugger attached to an existing process.
        /// 'attachForSuspendedLaunch': A project launcher component has launched a
        /// new process in a suspended state and then asked the debugger to attach.
        /// </summary>
        [JsonPropertyName("startMethod")]
        public string? StartMethod { get; set; }

        /// <summary>
        /// Gets or sets the size of a pointer or address for this process, in bits. This value
        /// may be used by clients when formatting addresses for display.
        /// </summary>
        [JsonPropertyName("pointerSize")]
        public int? PointerSize { get; set; }
    }
}
