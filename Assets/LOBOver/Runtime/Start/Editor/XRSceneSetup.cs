using System;
using System.Linq;
using LOBOver.Editor;
using LOBOver.Runtime;
using LOBOver.VR;
using LOBOver.MR;
using LOBOver.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace LOBOver.Start.Editor
{
    public static class XRSceneSetup
    {
        // Called by the existing builder through a single reflection boundary: its asmdef is outside WO-S4.
        public static void Build(SiteWorld world, DesktopPreview preview, WorldAssets assets)
        {
            var scene = world.gameObject.scene;
            var full = new GameObject("FullScaleRoot");
            foreach (var name in new[] { "Ground", "Road", "Gate", "Trailer", "JobSign", "SiteWorld", "SiteSky", "TrenchCorridor" })
                scene.GetRootGameObjects().Single(o => o.name == name).transform.SetParent(full.transform, true);
            var mr = new GameObject("MRDiorama"); mr.SetActive(false);
            var model = Object.Instantiate(full, mr.transform); model.name = "DioramaRoot";
            // Async StreamingAssets reads must never expose a full-size model on entry to MR.
            model.transform.localScale = Vector3.one * DioramaSeat.ScaleFor(DioramaSeat.SliceLengthFeet);
            var modelWorld = model.GetComponentInChildren<SiteWorld>(true);
            modelWorld.sky.enabled = false;
            // Clone the complete hierarchy in one operation so board/sky/truck references stay local.
            modelWorld.director = world.director;
            var before = model.transform.Find("Ground/BeforeTrench"); if (before != null) Object.DestroyImmediate(before.gameObject);
            var dome = modelWorld.sky.transform.Find("SkyInspectDome"); if (dome != null) Object.DestroyImmediate(dome.gameObject);
            InspectTarget.Attach(modelWorld.trailerBoard.gameObject, "board");
            InspectTarget.Attach(modelWorld.planPanel.gameObject, "board");
            var rig = CopySampleRig(scene);
            var center = rig.transform.Find("TrackingSpace/CenterEyeAnchor");
            var left = rig.transform.Find("TrackingSpace/LeftHandAnchor/LeftControllerAnchor");
            var right = rig.transform.Find("TrackingSpace/RightHandAnchor/RightControllerAnchor");
            if (left == null || right == null || center == null) throw new InvalidOperationException("SampleScene rig is missing controller/eye anchors");
            var manager = rig.GetComponent<OVRManager>();
            if (manager == null) throw new InvalidOperationException("SampleScene rig has no OVRManager");
            manager.trackingOriginType = OVRManager.TrackingOrigin.FloorLevel; manager.isInsightPassthroughEnabled = false;
            foreach (var listener in rig.GetComponentsInChildren<AudioListener>(true)) Object.DestroyImmediate(listener);
            foreach (var listener in preview.GetComponentsInChildren<AudioListener>(true)) Object.DestroyImmediate(listener);
            var listenerObject = WorldDrawing.Group("SeatListener", center); listenerObject.AddComponent<AudioListener>();
            foreach (var camera in rig.GetComponentsInChildren<Camera>(true))
            { camera.nearClipPlane = 0.01f; camera.farClipPlane = 6000; }
            // Keep the Editor menu at ordinary eye height without changing tracked poses on device.
            if (center.localPosition == Vector3.zero) center.localPosition = Vector3.up * 1.7f;
            var pointer = new GameObject("ControllerPointer").AddComponent<ControllerPointer>();
            pointer.rightHand = right; pointer.mouseCamera = center.GetComponent<Camera>(); pointer.assets = assets;
            var clip = SiteAssetBuilder.Required<AudioClip>("Assets/LOBGame/Audio/Construction/hydraulics_01.wav");
            var vr = new GameObject("VRSite"); vr.SetActive(false);
            var seat = vr.AddComponent<InspectorSeat>();
            seat.director = world.director; seat.world = world; seat.rig = rig; seat.pointer = pointer; seat.assets = assets; seat.clickClip = clip;
            seat.cab = BuildCab(vr.transform, assets, out var dashboard); seat.dashboard = dashboard;
            seat.wrist = Wrist("InspectorClipboard", left, assets, new Vector2(0.22f, 0.16f));
            seat.wrist.gameObject.SetActive(false);
            foreach (var oldFade in rig.GetComponentsInChildren<OVRScreenFade>(true)) Object.DestroyImmediate(oldFade);
            seat.fade = center.gameObject.AddComponent<ComfortFade>();
            var fadePath = SiteAssetBuilder.Prefabs + "ComfortFade.mat";
            var fadeMaterial = AssetDatabase.LoadAssetAtPath<Material>(fadePath);
            if (fadeMaterial == null)
            {
                var shader = Shader.Find("LOBOver/ComfortFade");
                if (shader == null) throw new InvalidOperationException("LOBOver/ComfortFade shader is not imported");
                fadeMaterial = new Material(shader); AssetDatabase.CreateAsset(fadeMaterial, fadePath);
            }
            seat.fade.material = fadeMaterial;
            seat.speech = vr.AddComponent<InspectionSpeech>();
            seat.voice = vr.AddComponent<SeatAudio>(); seat.voice.director = world.director; seat.voice.world = world;
            seat.voice.listener = center; seat.voice.dashboard = dashboard;
            seat.flagPrefab = Flag("InspectorFlag", assets, false); seat.stopFlagPrefab = Flag("StopWorkFlag", assets, true);
            var diorama = mr.AddComponent<DioramaSeat>();
            diorama.director = world.director; diorama.world = modelWorld; diorama.model = model.transform;
            diorama.eye = center; diorama.pointer = pointer; diorama.assets = assets; diorama.clickClip = clip;
            diorama.wrist = Wrist("DioramaControls", left, assets, new Vector2(0.28f, 0.17f)); diorama.wrist.gameObject.SetActive(false);
            diorama.voice = mr.AddComponent<SeatAudio>(); diorama.voice.director = world.director; diorama.voice.world = modelWorld;
            diorama.voice.listener = center; diorama.voice.dashboard = model.transform; diorama.voice.presenter = true;
            var passthrough = rig.GetComponent<OVRPassthroughLayer>() ?? rig.gameObject.AddComponent<OVRPassthroughLayer>();
            // SDK 207 OVRPassthroughLayer.SyncToOverlay enforces Underlay and the deepest composition order.
            passthrough.textureOpacity = 1; passthrough.edgeRenderingEnabled = false; passthrough.enabled = false;
            var start = new GameObject("StartPanel").AddComponent<StartPanel>();
            start.director = world.director; start.assets = assets; start.clickClip = clip; start.rig = rig; start.manager = manager;
            start.passthrough = passthrough; start.pointer = pointer; start.diorama = diorama;
            start.fullScaleRoot = full; start.vrRoot = vr; start.mrRoot = mr; start.desktopRoot = preview.gameObject; start.fullScalePlan = world.planPanel.gameObject;
            start.listener = listenerObject.transform; start.desktopEye = preview.previewCamera.transform; start.worldTruck = world.truck.gameObject;
            start.panel = WorldDrawing.Group("StartChoices", start.transform).transform;
            WorldDrawing.Box("Panel", start.panel, Vector3.zero, new Vector3(1.2f, 0.85f, 0.025f), assets.steel);
            // The Director is enabled only by StartPanel.Start; preserve its WO-S3 behaviour elsewhere.
            world.director.enabled = false;
        }
        private static OVRCameraRig CopySampleRig(Scene destination)
        {
            // Read the existing Building Blocks through Unity's scene API; never parse or edit scene YAML.
            var sample = EditorSceneManager.OpenPreviewScene("Assets/Scenes/SampleScene.unity");
            try
            {
                var source = sample.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<OVRCameraRig>(true)).Single();
                var copy = Object.Instantiate(source.gameObject); copy.name = "OVRCameraRig";
                SceneManager.MoveGameObjectToScene(copy, destination);
                // Controller tracking blocks normally live under the rig; preserve their exact settings.
                foreach (var controller in sample.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<OVRControllerHelper>(true)))
                {
                    if (controller.transform.IsChildOf(source.transform)) continue;
                    var left = controller.m_controller == OVRInput.Controller.LTouch;
                    var anchor = copy.transform.Find(left ? "TrackingSpace/LeftHandAnchor/LeftControllerAnchor" : "TrackingSpace/RightHandAnchor/RightControllerAnchor");
                    Object.Instantiate(controller.gameObject, anchor, false);
                }
                foreach (var layer in copy.GetComponentsInChildren<OVRPassthroughLayer>(true)) Object.DestroyImmediate(layer);
                return copy.GetComponent<OVRCameraRig>();
            }
            finally { EditorSceneManager.ClosePreviewScene(sample); }
        }
        private static Transform Wrist(string name, Transform anchor, WorldAssets assets, Vector2 size)
        {
            var root = WorldDrawing.Group(name, anchor).transform;
            root.localPosition = new Vector3(0, 0.05f, 0.08f); root.localRotation = Quaternion.Euler(60, 0, 0);
            WorldDrawing.Box("Board", root, Vector3.zero, new Vector3(size.x, size.y, 0.01f), assets.steel); return root;
        }
        private static Transform BuildCab(Transform parent, WorldAssets assets, out Transform dashboard)
        {
            var root = WorldDrawing.Group("SiteTruck", parent).transform;
            // WO-09 section 1: 2.2 x 2 x 2.6 m frame; the +Z front stays open.
            for (var x = -1; x <= 1; x += 2)
            {
                WorldDrawing.Box("RearPillar", root, new Vector3(x * 1.1f, 1, -1.3f), new Vector3(0.08f, 2, 0.08f), assets.steel);
                WorldDrawing.Box("FrontPillar", root, new Vector3(x * 1.1f, 1, 1.3f), new Vector3(0.08f, 2, 0.08f), assets.steel);
                WorldDrawing.Box("SideSill", root, new Vector3(x * 1.1f, 0.45f, 0), new Vector3(0.08f, 0.08f, 2.6f), assets.steel);
                WorldDrawing.Box("RoofRail", root, new Vector3(x * 1.1f, 2, 0), new Vector3(0.08f, 0.08f, 2.6f), assets.steel);
            }
            WorldDrawing.Box("Floor", root, new Vector3(0, 0.05f, 0), new Vector3(2.2f, 0.1f, 2.6f), assets.steel);
            WorldDrawing.Box("DriverSeat", root, new Vector3(0, 0.45f, 0), new Vector3(0.55f, 0.15f, 0.6f), assets.steel);
            WorldDrawing.Box("SeatBack", root, new Vector3(0, 0.75f, -0.3f), new Vector3(0.55f, 0.6f, 0.1f), assets.steel);
            dashboard = WorldDrawing.Group("Dashboard", root).transform; dashboard.localPosition = new Vector3(0, 1.1f, 0.8f);
            WorldDrawing.Box("Board", dashboard, Vector3.zero, new Vector3(0.8f, 0.3f, 0.04f), assets.steel);
            return root;
        }
        private static GameObject Flag(string name, WorldAssets assets, bool stopped)
        {
            var root = new GameObject(name);
            try
            {
                var material = assets.orange;
                if (stopped)
                {
                    var path = SiteAssetBuilder.Prefabs + "StopWorkFact.mat";
                    material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (material == null) { material = new Material(assets.orange); AssetDatabase.CreateAsset(material, path); }
                    material.SetColor("_BaseColor", new Color(0.8f, 0.06f, 0.04f)); EditorUtility.SetDirty(material);
                }
                WorldDrawing.Box("Pole", root.transform, new Vector3(0, 0.6f, 0), new Vector3(0.04f, 1.2f, 0.04f), assets.timber);
                // A stop-work flag records the inspector's action, never a judgement of that action.
                WorldDrawing.Box("Flag", root.transform, new Vector3(0.2f, 1, 0), new Vector3(0.4f, 0.25f, 0.02f), material);
                return PrefabUtility.SaveAsPrefabAsset(root, SiteAssetBuilder.Prefabs + name + ".prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
