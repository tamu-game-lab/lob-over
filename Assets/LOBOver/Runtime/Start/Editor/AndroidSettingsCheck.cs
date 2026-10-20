using System.Collections.Generic;
using System.Linq;
using LOBOver.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.XR.Management;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;

namespace LOBOver.Start.Editor
{
    public static class AndroidSettingsCheck
    {
        public static void Check()
        {
            var missing = Missing();
            foreach (var line in missing) Debug.LogWarning("LOB Over: " + line);
            if (missing.Count == 0) Debug.Log("LOB Over: Android settings check passed; no settings changed.");
        }
        public static List<string> Missing()
        {
            var missing = new List<string>();
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                missing.Add("Android: File > Build Profiles > Android > Switch Platform.");
            if (PlayerSettings.Android.targetArchitectures != AndroidArchitecture.ARM64)
                missing.Add("ARM64: Player Settings > Android > Other Settings > Target Architectures > ARM64 only.");
            if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) != ScriptingImplementation.IL2CPP)
                missing.Add("IL2CPP: Player Settings > Android > Other Settings > Scripting Backend > IL2CPP.");
            if ((int)PlayerSettings.Android.minSdkVersion < 32)
                missing.Add("Minimum SDK 32: Player Settings > Android > Other Settings > Minimum API Level > API 32.");
            var general = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
            if (general == null || general.Manager == null || !general.Manager.activeLoaders.Any(l => l is OpenXRLoader))
                missing.Add("OpenXR loader: Project Settings > XR Plug-in Management > Android > OpenXR.");
            var groups = OpenXRFeatureSetManager.FeatureSetsForBuildTarget(BuildTargetGroup.Android);
            var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            var quest = settings == null ? null : settings.GetFeature<MetaQuestFeature>();
            // OpenXR 1.18 exposes Meta Quest Support directly; older versions also have a named set.
            var questGroup = groups.FirstOrDefault(g => g.name.Contains("Meta Quest"));
            if (quest == null || !quest.enabled || questGroup != null && !questGroup.isEnabled)
                missing.Add("Meta Quest feature group: Project Settings > XR Plug-in Management > OpenXR > Android > Meta Quest Support.");
            var hasTarget = false;
            if (quest != null)
            {
                var targets = new SerializedObject(quest).FindProperty("targetDevices");
                if (targets != null)
                    for (var i = 0; i < targets.arraySize; i++)
                    {
                        var target = targets.GetArrayElementAtIndex(i);
                        if (target.FindPropertyRelative("manifestName").stringValue == "quest3s" && target.FindPropertyRelative("enabled").boolValue) hasTarget = true;
                    }
            }
            if (!hasTarget) missing.Add("Quest 3S target: Project Settings > XR Plug-in Management > OpenXR > Android > Meta Quest Support > Target Devices > Quest 3S.");
            var scenes = EditorBuildSettings.scenes;
            if (scenes.Length == 0 || scenes[0].path != SiteSceneBuilder.ScenePath || !scenes[0].enabled || AssetDatabase.LoadAssetAtPath<SceneAsset>(SiteSceneBuilder.ScenePath) == null)
                missing.Add("Site.unity first: File > Build Profiles > Scene List > put Assets/LOBOver/Scenes/Site.unity first and enable it (or LOB Over > Build > Site Scene).");
            if (EditorUserBuildSettings.androidBuildSubtarget != MobileTextureSubtarget.ASTC)
                missing.Add("ASTC: File > Build Profiles > Android > Texture Compression > ASTC.");
            return missing;
        }
    }
}
