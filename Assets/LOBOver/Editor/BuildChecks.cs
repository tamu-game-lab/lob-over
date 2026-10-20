using System;
using UnityEditor;
namespace LOBOver.Editor
{
    public static class BuildChecks
    {
        [MenuItem("LOB Over/Build/Check Android Settings")]
        public static void CheckAndroidSettings() =>
            Type.GetType("LOBOver.Start.Editor.AndroidSettingsCheck, LOBOver.XR.Editor", true)
                .GetMethod("Check").Invoke(null, null);
    }
}
