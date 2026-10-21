using System;
using System.Linq;
using LOBOver.Editor;
using LOBOver.Runtime;
using LOBOver.VR;
using LOBOver.MR;
using LOBOver.World;
using LOBOver.XR;
using Oculus.Interaction;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace LOBOver.Start.Editor
{
    public static class XRSceneSetup
    {
        // The builder passes only its owned world; block objects are discovered, never altered.
        public static void Build(SiteWorld world, DesktopPreview preview, WorldAssets assets)
        {
            var scene = world.gameObject.scene;
            var full = world.transform.parent.gameObject;
            var site = full.transform.parent;
            var hub = scene.GetRootGameObjects().Single(root => root.name == "[LOBOver] Hub").transform;
            var xr = world.director.GetComponent<XRRig>(); xr.Resolve(false); preview.xr = xr;
            var mr = WorldDrawing.Group("MRDiorama", hub); mr.SetActive(false);
            var model = Object.Instantiate(full, mr.transform); model.name = "DioramaRoot";
            model.transform.localScale = Vector3.one * DioramaSeat.ScaleFor(DioramaSeat.SliceLengthFeet);
            var modelWorld = model.GetComponentInChildren<SiteWorld>(true); modelWorld.sky.enabled = false;
            modelWorld.director = world.director;
            var before = model.transform.Find("Ground/BeforeTrench"); if (before != null) Object.DestroyImmediate(before.gameObject);
            var dome = modelWorld.sky.transform.Find("SkyInspectDome"); if (dome != null) Object.DestroyImmediate(dome.gameObject);
            InspectTarget.Attach(modelWorld.trailerBoard.gameObject, "board");
            InspectTarget.Attach(modelWorld.planPanel.gameObject, "board");
            var clip = SiteAssetBuilder.Required<AudioClip>("Assets/LOBGame/Audio/Construction/hydraulics_01.wav");
            var vr = WorldDrawing.Group("VRSite", site); vr.SetActive(false);
            var seat = vr.AddComponent<InspectorSeat>();
            seat.director = world.director; seat.world = world; seat.xr = xr; seat.siteSpace = site; seat.assets = assets; seat.clickClip = clip;
            seat.cab = BuildCab(vr.transform, assets, out var dashboard); seat.dashboard = dashboard;
            seat.wrist = Wrist("InspectorClipboard", vr.transform, xr, assets, new Vector2(0.22f, 0.16f));
            var fadeObject = WorldDrawing.Group("ComfortMask", vr.transform);
            fadeObject.AddComponent<RigAnchorFollower>().rig = xr;
            seat.fade = fadeObject.AddComponent<ComfortFade>();
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
            seat.voice.listener = xr.CentreEye; seat.voice.dashboard = dashboard;
            seat.flagPrefab = Flag("InspectorFlag", assets, false); seat.stopFlagPrefab = Flag("StopWorkFlag", assets, true);
            var diorama = mr.AddComponent<DioramaSeat>();
            diorama.director = world.director; diorama.world = modelWorld; diorama.model = model.transform;
            diorama.xr = xr; diorama.assets = assets; diorama.clickClip = clip;
            diorama.wrist = Wrist("DioramaControls", mr.transform, xr, assets, new Vector2(0.28f, 0.17f));
            diorama.voice = mr.AddComponent<SeatAudio>(); diorama.voice.director = world.director; diorama.voice.world = modelWorld;
            diorama.voice.listener = xr.CentreEye; diorama.voice.dashboard = model.transform; diorama.voice.presenter = true;
            if (!XRRig.Components<AudioListener>(scene).Any(listener => listener.isActiveAndEnabled))
            {
                var listener = WorldDrawing.Group("SeatListener", world.director.transform);
                listener.AddComponent<AudioListener>(); listener.AddComponent<RigAnchorFollower>().rig = xr;
            }
            if (!XRRig.Components<PointableCanvasModule>(scene).Any(module => module.isActiveAndEnabled))
            {
                var events = WorldDrawing.Group("UIEvents", world.director.transform);
                events.AddComponent<EventSystem>(); events.AddComponent<PointableCanvasModule>();
            }
            var start = new GameObject("[LOBOver] Start").AddComponent<StartPanel>();
            start.director = world.director; start.assets = assets; start.clickClip = clip; start.xr = xr; start.diorama = diorama;
            start.fullScaleRoot = full; start.vrRoot = vr; start.mrRoot = mr; start.desktopRoot = preview.gameObject;
            start.fullScalePlan = world.planPanel.gameObject; start.worldTruck = world.truck.gameObject; start.preview = preview;
            var canvasObject = new GameObject("StartChoices", typeof(RectTransform)); canvasObject.transform.SetParent(start.transform, false);
            var canvas = canvasObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            var rect = (RectTransform)canvas.transform; rect.sizeDelta = new Vector2(1200, 850);
            rect.localScale = Vector3.one * 0.0005f;
            canvasObject.AddComponent<GraphicRaycaster>();
            canvasObject.AddComponent<Image>().color = new Color(0.12f, 0.15f, 0.17f, 1);
            InteractionSurface.Canvas(canvas); start.panel = rect;
            world.director.enabled = false;
        }
        private static Transform Wrist(string name, Transform parent, XRRig xr, WorldAssets assets, Vector2 size)
        {
            var root = WorldDrawing.Group(name, parent).transform;
            var follower = root.gameObject.AddComponent<RigAnchorFollower>(); follower.rig = xr; follower.leftWrist = true;
            follower.positionOffset = new Vector3(0, 0.05f, 0.08f); follower.rotationOffset = new Vector3(60, 0, 0);
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
