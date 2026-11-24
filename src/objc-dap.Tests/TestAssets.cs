namespace ObjcDap.Tests
{
    internal static class TestAssets
    {
        public static string GetTargetPath(string name)
        {
            var suffix = OperatingSystem.IsWindows() ? ".exe" : "";

            var path =
                Path.GetFullPath(
                    Path.Combine(
                        GetSourceRoot(),
                        $"libobjcd/build/targets/{name}{suffix}"));

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
            // The output depth varies by platform (bin/x64/Debug/... vs. bin/Debug/...), so search upwards.
            for (var directory = new DirectoryInfo(Environment.CurrentDirectory); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "global.json")))
                {
                    return directory.FullName;
                }
            }

            throw new InvalidOperationException($"Could not find source root from {Environment.CurrentDirectory}");
        }
    }
}
