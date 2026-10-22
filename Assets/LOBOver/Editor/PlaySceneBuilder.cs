using System;
using System.IO;
using System.Linq;
using LOBOver.Client;
using LOBOver.Play;
using LOBOver.World;
using LOBOver.XR;
using Meta.XR.BuildingBlocks;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace LOBOver.Editor
{
    public static class PlaySceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Play.unity"; // WO-L2 Amendment 1; Bryan owns this scene.
        private const float FloorSize = 20; // Existing tracked-floor guard, retained from WO-S6.
        [MenuItem("LOB Over/Build/Play Scene")]
        public static void BuildMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }
        public static Scene OpenExisting()
        {
            if (!File.Exists(ScenePath)) throw new FileNotFoundException("Bryan must save the Building Blocks scene at " + ScenePath);
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SceneManager.SetActiveScene(scene); return scene;
        }
        public static Scene Build(bool saveScene = true)
        {
            var scene = OpenExisting();
            if (!BuildingBlocksSetup.Check(scene)) throw new InvalidOperationException("Play Scene stopped: resolve the Building Blocks check first.");
            var replaced = scene.GetRootGameObjects().Where(r => r.name.StartsWith("[LOBOver]", StringComparison.Ordinal)).ToArray();
            foreach (var root in replaced)
                if (root.GetComponentInChildren<BuildingBlock>(true) != null || root.GetComponentInChildren<OVRCameraRig>(true) != null
                    || root.GetComponentsInChildren<Transform>(true).Any(t => t.name.StartsWith("[BuildingBlock]", StringComparison.Ordinal) || t.name == "OVRCameraRig"))
                    throw new InvalidOperationException("Play Scene stopped: protected block/rig under " + root.name + ". Nothing removed.");
            // WO-L4 owns new copies under Art; the legacy prefab directory is outside this order.
            var assets = SitePresentationBuilder.BuildAssets();
            var dark = Material("PlayPanel", "Universal Render Pipeline/Unlit", new Color(0.035f, 0.045f, 0.06f, 1));
            var buttonMaterial = Material("PlayButton", "Universal Render Pipeline/Unlit", new Color(0.18f, 0.22f, 0.28f, 1));
            var warpMaterial = Material("Warp", "LOBOver/Warp", Color.white, false);
            var fadeMaterial = Material("WarpFade", "LOBOver/WarpFade", Color.black);
            foreach (var root in replaced) Object.DestroyImmediate(root);
            var lighting = Root("Lighting");
            var sun = WorldDrawing.Group("Sun", lighting).AddComponent<Light>(); sun.type = LightType.Directional;
            SitePresentationBuilder.Lighting(sun);
            foreach (var light in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Light>(true)).Where(l => l != sun && l.enabled))
            {
                if (light.GetComponentInParent<BuildingBlock>(true) != null
                    || light.GetComponentsInParent<Transform>(true).Any(t => t.name.StartsWith("[BuildingBlock]", StringComparison.Ordinal)))
                    throw new InvalidOperationException("WO-L4: protected Building Block light would exceed the one-light budget: " + light.name);
                light.enabled = false; EditorUtility.SetDirty(light); PrefabUtility.RecordPrefabInstancePropertyModifications(light);
                Debug.Log("WO-L4: disabled extra scene light " + light.name + "; the site uses one directional sun.");
            }
            var hub = Root("Hub");
            var floor = WorldDrawing.Group("TrackedFloor", hub).AddComponent<BoxCollider>();
            floor.size = new Vector3(FloorSize, 0.1f, FloorSize); floor.center = new Vector3(0, -0.05f, 0);
            var site = Root("Site"); site.gameObject.SetActive(false);
            var sky = SitePresentationBuilder.Build(site, assets);
            var world = WorldDrawing.Group("SiteWorld", site).AddComponent<SiteWorld>(); world.assets = assets;
            world.sky = sky;
            var client = Root("Client");
            var connection = client.gameObject.AddComponent<HostConnection>();
            var xr = client.gameObject.AddComponent<XRRig>(); client.gameObject.AddComponent<XRRuntimeSettings>();
            var presenter = client.gameObject.AddComponent<HostWorldPresenter>(); presenter.connection = connection; presenter.world = world;
            var warpRoot = Root("Warp"); var warp = warpRoot.gameObject.AddComponent<WarpController>();
            warp.connection = connection; warp.rig = xr; warp.presenter = presenter; warp.site = site; warp.hub = hub.gameObject;
            warp.dissolveMaterial = warpMaterial; warp.fallbackMaterial = fadeMaterial;
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere); sphere.name = "WarpSphere"; sphere.transform.SetParent(warpRoot, false);
            Object.DestroyImmediate(sphere.GetComponent<Collider>()); sphere.transform.localScale = Vector3.one * WarpController.SphereDiameter;
            warp.sphere = sphere.GetComponent<Renderer>(); warp.sphere.sharedMaterial = warpMaterial ?? fadeMaterial;
            warp.sphere.shadowCastingMode = ShadowCastingMode.Off; warp.sphere.receiveShadows = false; sphere.SetActive(false);
            var panel = WorldDrawing.Group("CrewPanel", hub).transform;
            var background = WorldDrawing.Box("Background", panel, Vector3.zero, new Vector3(HubPanel.Width, 0.43f, 0.008f), dark);
            Object.DestroyImmediate(background.GetComponent<Collider>());
            Label("Title", panel, "LOB, Over", new Vector3(0, 0.17f, -0.008f), new Vector2(0.36f, 0.04f), HubPanel.TitleHeight, assets);
            var status = Label("Status", panel, "Looking for the game. Start it on the PC with Play LOB Over.",
                new Vector3(0, 0.09f, -0.008f), new Vector2(0.36f, 0.1f), HubPanel.TextHeight, assets);
            var hubPanel = client.gameObject.AddComponent<HubPanel>(); hubPanel.connection = connection; hubPanel.rig = xr; hubPanel.panel = panel; hubPanel.status = status;
            var click = SiteAssetBuilder.Required<AudioClip>(SiteAssetBuilder.Art + "Click.wav");
            var names = new[] { "Ray · digging", "Marcus · pipe", "Dana · backfill" };
            for (var i = 0; i < names.Length; i++)
                Button(panel, names[i], WorldUnits.CrewIds[i], new Vector3(0, -0.02f - i * 0.075f, -0.012f),
                    new Vector2(HubPanel.ButtonWidth, HubPanel.ButtonHeight), assets, buttonMaterial, click, warp);
            var wrist = WorldDrawing.Group("BackWrist", warpRoot);
            var follower = wrist.AddComponent<RigAnchorFollower>(); follower.rig = xr; follower.leftWrist = true;
            follower.positionOffset = new Vector3(0, 0.08f, 0); follower.rotationOffset = new Vector3(90, 0, 0);
            var back = Button(wrist.transform, "Back", "", Vector3.zero, new Vector2(0.11f, 0.06f), assets, buttonMaterial, click, warp);
            warp.wrist = wrist; wrist.SetActive(false);
            var voice = client.gameObject.AddComponent<HostVoice>(); voice.connection = connection; voice.world = world; voice.warp = warp;
            voice.source = WorldDrawing.Sound(WorldDrawing.Group("ForemanVoice", client), false, HostVoice.SilentMetres);
            voice.source.minDistance = HostVoice.ClearMetres;
            var preview = Root("Preview"); preview.gameObject.tag = "EditorOnly";
            var camera = WorldDrawing.Group("DesktopCamera", preview).AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 1.65f, 0); camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear; camera.allowHDR = false; camera.nearClipPlane = 0.05f; camera.farClipPlane = 6000; camera.depth = 100;
            var desktop = preview.gameObject.AddComponent<PlayPreview>(); desktop.view = camera; desktop.rig = xr; desktop.back = back;
            xr.editorHead = camera.transform;
            sky.observer = camera.transform;
            sky.rig = xr;
            EditorSceneManager.MarkSceneDirty(scene);
            if (saveScene)
            {
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save " + ScenePath);
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            }
            AssetDatabase.SaveAssets();
            Debug.Log("LOB Over: Play Scene built; Sync Data before the Quest build. Start Play LOB Over on the PC.");
            return scene;
        }
        private static Transform Root(string name) => new GameObject("[LOBOver] " + name).transform;
        private static Material Material(string name, string shaderName, Color color, bool required = true)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null) { if (!required) return null; throw new InvalidOperationException("Missing shader " + shaderName); }
            var path = SiteAssetBuilder.Art + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.shader = shader;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material); return material;
        }
        private static TextMeshPro Label(string name, Transform parent, string value, Vector3 position, Vector2 size, float height, WorldAssets assets)
        {
            var label = WorldDrawing.Text(name, parent, position, size, height, assets.font);
            // TMP uses a tenth of a world unit per font unit. Size the capital glyph, not its line box.
            var face = assets.font.faceInfo;
            label.fontSize = height * 10 * face.pointSize / Mathf.Max(1, face.capLine);
            label.alignment = TextAlignmentOptions.Center; label.text = value; return label;
        }
        private static CrewButton Button(Transform parent, string title, string crew, Vector3 position, Vector2 size,
            WorldAssets assets, Material material, AudioClip click, WarpController warp)
        {
            var obj = WorldDrawing.Group(string.IsNullOrEmpty(crew) ? "Back" : "Visit_" + crew, parent);
            obj.transform.localPosition = position;
            WorldDrawing.Box("Face", obj.transform, Vector3.zero, new Vector3(size.x, size.y, 0.006f), material);
            Label("Label", obj.transform, title, new Vector3(0, 0, -0.005f), size * 0.95f, HubPanel.TextHeight, assets);
            InteractionSurface.Button(obj, size);
            var button = obj.AddComponent<CrewButton>(); button.warp = warp; button.crew = crew;
            button.click = obj.AddComponent<AudioSource>(); button.click.playOnAwake = false; button.click.spatialBlend = 0; button.click.clip = click;
            return button;
        }
    }
}
