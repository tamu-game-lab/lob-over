using System.IO;
using UnityEditor;
using UnityEngine;

namespace LOBOver.Editor
{
    public static class DataSync
    {
        [MenuItem("LOB Over/Build/Sync Data")]
        public static void Sync()
        {
            var root = Directory.GetParent(Application.dataPath).FullName;
            Copy(Path.Combine(root, "Data/catalog.json"), "Data/catalog.json");
            CopyFiles(Path.Combine(root, "Data/Scenarios"), "*.json", "Data/Scenarios", false);
            CopyFiles(Path.Combine(root, "Monitor/test/scripts"), "*.json", "Data/scripts", false);
            CopyFiles(Path.Combine(root, "Voice"), "*.wav", "Voice", true);
            AssetDatabase.Refresh();
            Debug.Log("LOB Over: catalog, scenarios, scripts and voice synced to StreamingAssets.");
        }
        private static void CopyFiles(string source, string pattern, string target, bool recursive)
        {
            foreach (var file in Directory.GetFiles(source, pattern, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
                Copy(file, target + "/" + file.Substring(source.Length + 1).Replace('\\', '/'));
        }
        private static void Copy(string source, string relative)
        {
            var target = Path.Combine(Application.streamingAssetsPath, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)); File.Copy(source, target, true);
        }
    }
}
