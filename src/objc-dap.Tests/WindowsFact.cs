using System.Runtime.CompilerServices;

namespace ObjcDap.Tests
{
    public class WindowsFact : FactAttribute
    {
        public WindowsFact(
            [CallerFilePath] string? sourceFilePath = null,
            [CallerLineNumber] int sourceLineNumber = -1)
              : base(sourceFilePath, sourceLineNumber)
        {
            if (!OperatingSystem.IsWindows())
            {
                Skip = "Test only runs on Windows.";
            }
        }
    }
}
