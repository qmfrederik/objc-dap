namespace Objc.Dap
{
    public class DebugAdapterOptions
    {
        /// <summary>
        /// Gets or sets the the path to the <c>lldb-dap</c> executable.
        /// </summary>
        public required FileInfo LldbDapPath { get; set; }

        /// <summary>
        /// Gets or sets the input (client to objc-dap) stream for the debug adapter.
        /// </summary>
        public required Stream Input { get; set; }

        /// <summary>
        /// Gets or sets the output stream (objc-dap to client) for the debug adapter.
        /// </summary>
        public required Stream Output { get; set; }
        
        /// <summary>
        /// Gets or sets the error stream (objc-dap to client) for the debug adapter.
        /// </summary>
        public required Stream Error { get; set; }

        /// <summary>
        /// Gets or sets, the path to the Python 3 library.  Will be added to the PATH environment
        /// variables for the debug adapter process.
        /// </summary>
        public FileInfo? Python3Library { get; set; }
    }
}
