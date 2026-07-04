using System.IO;
using UnityEngine;

namespace SCZip.Core
{
    public static class AppPaths
    {
        /// <summary>Directory containing the player executable (or project root in Editor).</summary>
        public static string GetExecutableDirectory()
        {
            var dataPath = Application.dataPath;
            var dir = Directory.GetParent(dataPath)?.FullName;
            return string.IsNullOrEmpty(dir) ? dataPath : dir;
        }
    }
}
