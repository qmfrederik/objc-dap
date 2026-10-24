using System.CommandLine;

namespace Objc.Dap
{
    public class Program
    {
        public static int Main(string[] args)
        {
            RootCommand rootCommand = new("Debug Adapter for Objective-C applications");
            rootCommand.SetAction(Run);

            return rootCommand.Parse(args).Invoke();
        }

        public static int Run(ParseResult parseResult)
        {
            return 0;
        }
    }
}