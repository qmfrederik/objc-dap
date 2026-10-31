namespace ObjcDap.Tests
{
    internal static class TestAssets
    {
        public static string GetTargetPath(string name)
        {
            var path =
                Path.GetFullPath(
                    Path.Combine(
                        GetSourceRoot(),
                        $"libobjcd/build/targets/{name}.exe"));

            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    $"Native test target '{name}' was not built. Make sure it is registered in src/libobjcd/targets/CMakeLists.txt.",
                    path);
            }

            return path;
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
