using System.Runtime.CompilerServices;

namespace ObjcDap.Tests
{
    public class LinuxFact : FactAttribute
    {
        public LinuxFact(
            [CallerFilePath] string? sourceFilePath = null,
            [CallerLineNumber] int sourceLineNumber = -1)
              : base(sourceFilePath, sourceLineNumber)
        {
            if (!OperatingSystem.IsLinux())
            {
                Skip = "Test only runs on Linux.";
            }
        }
    }
}
