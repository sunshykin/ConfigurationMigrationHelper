using System;
using System.IO;

namespace MigrationHelper
{
    public static class PathHelper
    {
        private const string ProjectName = "MigrationHelper";

        private static string GetSourceDirectoryPath()
        {
            var binFolder = Directory.GetCurrentDirectory();
            var index = binFolder.IndexOf(ProjectName, StringComparison.InvariantCulture) + ProjectName.Length;

            return binFolder.Substring(0, index);
        }
        
        public static string CombineRelativePath(string path1)
        {
            return Path.GetFullPath(Path.Combine(GetSourceDirectoryPath(), path1));
        }
        
        public static string CombineRelativePath(string path1, string path2)
        {
            return Path.GetFullPath(Path.Combine(GetSourceDirectoryPath(), path1, path2));
        }
    }
}