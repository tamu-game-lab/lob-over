using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LOBOver.XR;
using Meta.XR.BuildingBlocks;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using Oculus.Interaction.Locomotion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LOBOver.Editor
{
    public static class BuildingBlocksSetup
    {
        // SDK core and interaction.ovr Editor/BlockDataIds.cs; names alone never establish block presence.
        private const string InteractionId = "81f55626-5fad-45e9-a1df-184f330da7ba";
        public static readonly string[] DemoNames = { "[BuildingBlock] Cube", "Poke Interaction" };

        [MenuItem("LOB Over/Setup/Check Building Blocks")]
        public static void CheckMenu() => CheckSite();

        public static bool CheckSite()
        {
            if (!File.Exists(SiteSceneBuilder.ScenePath))
            { Debug.LogError("LOB Over: Site.unity is missing; run Site Scene, then add the blocks listed by Check Building Blocks."); return false; }
            var scene = SceneManager.GetSceneByPath(SiteSceneBuilder.ScenePath);
            var preview = !scene.IsValid() || !scene.isLoaded;
            if (preview) scene = EditorSceneManager.OpenPreviewScene(SiteSceneBuilder.ScenePath);
            try { return Check(scene); }
            finally { if (preview) EditorSceneManager.ClosePreviewScene(scene); }
        }

        public static List<string> Missing(Scene scene)
        {
            var missing = new List<string>();
            var rig = XRRig.Components<OVRCameraRig>(scene).FirstOrDefault();
            var blocks = XRRig.Components<BuildingBlock>(scene);
            void Need(bool present, string name) { if (!present) missing.Add(name); }
            Need(rig != null && rig.centerEyeAnchor != null && rig.GetComponent<OVRManager>() != null, "Camera Rig");
            Need(XRRig.Components<OVRPassthroughLayer>(scene).Length > 0, "Passthrough");
            foreach (var side in new[] { Handedness.Left, Handedness.Right })
            {
                var controller = XRRig.Components<FromOVRControllerDataSource>(scene).Any(c => HasSide(c, side))
                    || XRRig.Components<OVRControllerHelper>(scene).Any(c => c.m_controller == (side == Handedness.Left ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch));
                Need(controller && rig != null && (side == Handedness.Left ? rig.leftControllerAnchor : rig.rightControllerAnchor) != null,
                    "Controller Tracking (" + side.ToString().ToLowerInvariant() + ")");
            }
            Need((blocks.Any(block => block.BlockId == InteractionId) || XRRig.Components<RayInteractor>(scene).Length > 0)
                && XRRig.Components<RayInteractor>(scene).Length > 0 && XRRig.Components<PokeInteractor>(scene).Length > 0, "Interactions Rig");
            foreach (var side in new[] { Handedness.Left, Handedness.Right })
            {
                var hand = XRRig.Components<FromOVRHandDataSource>(scene).Any(c => HasSide(c, side))
                    || XRRig.Components<OVRHand>(scene).Any(c => c.GetHand() == (side == Handedness.Left ? OVRPlugin.Hand.HandLeft : OVRPlugin.Hand.HandRight));
                Need(hand && rig != null && (side == Handedness.Left ? rig.leftHandAnchor : rig.rightHandAnchor) != null,
                    "Hand Tracking (" + side.ToString().ToLowerInvariant() + ")");
            }
            Need(blocks.Any(block => block.BlockId == XRRig.RealHandsId), "Real Hands");
            return missing;
        }
        private static bool HasSide(Component source, Handedness side)
        {
            using var serialized = new SerializedObject(source);
            return serialized.FindProperty("_handedness")?.intValue == (int)side;
        }
        public static bool Check(Scene scene)
        {
            var missing = Missing(scene);
            foreach (var name in new[] { "Camera Rig", "Passthrough", "Controller Tracking (left)", "Controller Tracking (right)",
                "Interactions Rig", "Hand Tracking (left)", "Hand Tracking (right)", "Real Hands" })
            {
                if (missing.Contains(name)) Debug.LogError("LOB Over: MISSING " + name + "; add it from Building Blocks.");
                else Debug.Log("LOB Over: PRESENT " + name);
            }
            foreach (var layer in XRRig.Components<OVRPassthroughLayer>(scene))
                Debug.Log("LOB Over: Passthrough layer " + PathOf(layer.transform) + (layer.GetComponent<OVRCameraRig>() != null ? " (additional rig layer; preserved)" : ""));
            foreach (var demo in Demos(scene)) Debug.Log("LOB Over: block demo " + PathOf(demo.transform) + "; active=" + demo.activeSelf);
            return missing.Count == 0;
        }
        public static IEnumerable<GameObject> Demos(Scene scene) => scene.GetRootGameObjects().Where(root => DemoNames.Contains(root.name));

        [MenuItem("LOB Over/Setup/Configure Building Blocks")]
        public static void ConfigureMenu()
        {
            if (!File.Exists(SiteSceneBuilder.ScenePath)) { CheckSite(); return; }
            var scene = SiteSceneBuilder.OpenExisting();
            if (!Check(scene)) return;
            Configure(scene);
            EditorSceneManager.SaveScene(scene);
        }
        public static void Configure(Scene scene)
        {
            foreach (var component in XRRig.Components<Behaviour>(scene))
            {
                // Include the SDK's event broadcasters/connections: enabled listeners could still move a disabled locomotor.
                if (component != null && component.GetType().Namespace == "Oculus.Interaction.Locomotion" && BelongsToBlocks(component)) // missing-script components come back null
                {
                    if (component is FirstPersonLocomotor locomotor && locomotor.GravityFactor != 0)
                        Change(locomotor, "GravityFactor", locomotor.GravityFactor, 0f, () => locomotor.GravityFactor = 0);
                    if (component.enabled) Change(component, "enabled", true, false, () => component.enabled = false);
                }
            }
            foreach (var manager in XRRig.Components<OVRManager>(scene).Where(BelongsToBlocks))
            {
                if (manager.trackingOriginType != OVRManager.TrackingOrigin.FloorLevel)
                    Change(manager, "trackingOriginType", manager.trackingOriginType, OVRManager.TrackingOrigin.FloorLevel,
                        () => manager.trackingOriginType = OVRManager.TrackingOrigin.FloorLevel);
                if (!manager.isInsightPassthroughEnabled)
                    Change(manager, "isInsightPassthroughEnabled", false, true, () => manager.isInsightPassthroughEnabled = true);
            }
            foreach (var layer in XRRig.Components<OVRPassthroughLayer>(scene).Where(l => BelongsToBlocks(l) && l.GetComponent<OVRCameraRig>() == null)) // the Passthrough block's layer; the rig's own is an old-builder leftover
            {
                if (!layer.enabled) Change(layer, "enabled", false, true, () => layer.enabled = true);
                if (layer.hidden) Change(layer, "hidden", true, false, () => layer.hidden = false);
                // Installed SDK 207 SyncToOverlay always sets Underlay/int.MaxValue; it exposes no overlay-type field.
                Debug.Log("LOB Over: " + PathOf(layer.transform) + " uses SDK-enforced Underlay (SyncToOverlay); preserved.");
            }
            foreach (var demo in Demos(scene))
                if (demo.activeSelf) Change(demo, "activeSelf", true, false, () => demo.SetActive(false));
        }
        private static bool BelongsToBlocks(Component component) => component.GetComponentInParent<BuildingBlock>(true) != null
            || component.GetComponentInParent<OVRCameraRig>(true) != null;
        private static void Change(UnityEngine.Object target, string property, object before, object after, Action apply)
        {
            Undo.RecordObject(target, "Configure Building Blocks"); apply(); EditorUtility.SetDirty(target);
            var component = target as Component;
            var owner = component == null ? (GameObject)target : component.gameObject;
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            EditorSceneManager.MarkSceneDirty(owner.scene);
            Debug.Log("LOB Over: " + PathOf(owner.transform) + "/" + target.GetType().Name + "." + property + ": " + before + " -> " + after);
        }
        private static string PathOf(Transform target) => target.parent == null ? target.name : PathOf(target.parent) + "/" + target.name;
    }
}
