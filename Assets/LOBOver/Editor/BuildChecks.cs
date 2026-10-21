using System;
using System.Collections.Generic;
using UnityEditor;
namespace LOBOver.Editor
{
    public static class BuildChecks
    {
        public static bool Passes(out string report)
        {
            var missing = (List<string>)Type.GetType("LOBOver.Start.Editor.AndroidSettingsCheck, LOBOver.XR.Editor", true)
                .GetMethod("Missing").Invoke(null, null);
            report = string.Join(Environment.NewLine, missing);
            return missing.Count == 0;
        }

        [MenuItem("LOB Over/Build/Check Android Settings")]
        public static void CheckAndroidSettings() =>
            Type.GetType("LOBOver.Start.Editor.AndroidSettingsCheck, LOBOver.XR.Editor", true)
                .GetMethod("Check").Invoke(null, null);
    }
}
