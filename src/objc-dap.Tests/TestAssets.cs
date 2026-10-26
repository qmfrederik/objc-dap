namespace ObjcDap.Tests
{
    internal static class TestAssets
    {
        public static string GetTargetPath(string name)
        {
            return
                Path.GetFullPath(
                    Path.Combine(
                        GetSourceRoot(),
                        $"libobjcd/build/targets/{name}.exe"));
        }

        public static string GetSourcePath(string name)
        {
            return
                Path.GetFullPath(
                    Path.Combine(
                        GetSourceRoot(),
                        $"libobjcd/targets/",
                        name));
        }

        private static string GetSourceRoot()
        {
            var source =
                Path.GetFullPath(
                    Path.Combine(
                    Environment.CurrentDirectory,
                    $"../../../../../"));

            if (!File.Exists(Path.Combine(source, "global.json")))
            {
                throw new InvalidOperationException($"Could not find source root from {Environment.CurrentDirectory}");
            }

            return source;
        }
    }
}
