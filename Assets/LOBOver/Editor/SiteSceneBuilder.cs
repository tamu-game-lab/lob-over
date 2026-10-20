using System;
using System.IO;
using LOBOver.Runtime;
using LOBOver.World;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace LOBOver.Editor
{
    public static class SiteSceneBuilder
    {
        public const string ScenePath = "Assets/LOBOver/Scenes/Site.unity";
        [MenuItem("LOB Over/Build/Site Scene")]
        public static void BuildMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }
        public static Scene Build(bool saveScene = true)
        {
            DataSync.Sync(); var assets = SiteAssetBuilder.Build();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var light = new GameObject("Sun").AddComponent<Light>(); light.type = LightType.Directional;
            light.color = new Color(1, 0.94f, 0.82f); light.intensity = 1.2f; light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(48, -35, 0); RenderSettings.sun = light;
            RenderSettings.ambientMode = AmbientMode.Trilight; RenderSettings.ambientSkyColor = new Color(0.65f, 0.75f, 0.85f);
            RenderSettings.ambientEquatorColor = new Color(0.48f, 0.5f, 0.52f); RenderSettings.ambientGroundColor = new Color(0.25f, 0.23f, 0.2f);
            RenderSettings.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
            var volume = new GameObject("Global Volume").AddComponent<Volume>(); volume.isGlobal = true;
            volume.sharedProfile = SiteAssetBuilder.Required<VolumeProfile>("Assets/Settings/DefaultVolumeProfile.asset");
            BuildGround(assets);
            var gate = new GameObject("Gate"); gate.transform.position = new Vector3(-20, 0, WorldUnits.RoadZ);
            for (var i = 0; i < 2; i++)
            {
                var barrier = (GameObject)PrefabUtility.InstantiatePrefab(assets.barrier); barrier.name = "GateBarrier_" + (i + 1);
                barrier.transform.SetParent(gate.transform, false); barrier.transform.localPosition = new Vector3(0, 0, i == 0 ? -3.5f : 3.5f);
            }
            var cone = (GameObject)PrefabUtility.InstantiatePrefab(assets.cone); cone.name = "GateCone";
            cone.transform.SetParent(gate.transform, false); cone.transform.localPosition = new Vector3(0, 0, 2.5f);
            var ticket = WorldDrawing.Box("GateTicket", gate.transform, new Vector3(0, 1.5f, 3), new Vector3(0.7f, 0.5f, 0.04f), assets.steel);
            InspectTarget.Attach(ticket, "ticket");
            var gateText = WorldDrawing.Text("GateText", gate.transform, new Vector3(0, 1.5f, 2.96f), new Vector2(0.65f, 0.45f), 0.08f, assets.font);
            var trailer = WorldDrawing.Box("Trailer", null, new Vector3(-15, 1.5f, 14), new Vector3(6, 3, 3), SiteAssetBuilder.Material("Repair_SiteCabin"));
            var siteBoard = Board("TrailerBoard", new Vector3(-15, 1.9f, 12.44f), new Vector2(2, 1), assets).AddComponent<TrailerBoard>();
            var planBoard = Board("PlanPanel", new Vector3(-15, 1.9f, 15.56f), new Vector2(3, 1.5f), assets).AddComponent<PlanPanel>();
            planBoard.transform.rotation = Quaternion.Euler(0, 180, 0);
            // Boards remain at unit scale; parenting preserves their world dimensions.
            siteBoard.transform.SetParent(trailer.transform, true); planBoard.transform.SetParent(trailer.transform, true);
            var sign = new GameObject("JobSign"); sign.transform.position = new Vector3(-20, 0, 10);
            for (var side = -1; side <= 1; side += 2) WorldDrawing.Box("TimberPost", sign.transform, new Vector3(side * 0.45f, 0.9f, 0), new Vector3(0.08f, 1.8f, 0.08f), assets.timber);
            WorldDrawing.Box("SignBoard", sign.transform, new Vector3(0, 1.6f, 0), new Vector3(1.2f, 0.8f, 0.05f), assets.steel);
            var jobSign = sign.AddComponent<JobSign>(); jobSign.label = WorldDrawing.Text("Job", sign.transform, new Vector3(0, 1.6f, -0.04f), new Vector2(1.1f, 0.7f), 0.1f, assets.font);
            var director = new GameObject("Director").AddComponent<Director>();
            var world = new GameObject("SiteWorld").AddComponent<SiteWorld>(); world.director = director; world.assets = assets;
            world.trailerBoard = siteBoard; world.planPanel = planBoard; world.jobSign = jobSign; world.gateText = gateText;
            world.truck = new GameObject("SiteTruck").transform; world.truck.SetParent(world.transform, false); world.truck.localPosition = new Vector3(0, 0, WorldUnits.RoadZ);
            var truckFrame = WorldDrawing.Box("TruckFrame", world.truck, new Vector3(-1, 0.55f, 0), new Vector3(2.2f, 0.3f, 2), assets.steel);
            // Preview marker only; the XR cab and its comfort presentation belong to WO-S4.
            var previewPrefab = BuildPreviewPrefab();
            var preview = ((GameObject)PrefabUtility.InstantiatePrefab(previewPrefab)).GetComponent<DesktopPreview>(); preview.name = "DesktopPreview";
            preview.director = director; preview.world = world;
            preview.previewCamera.transform.position = new Vector3(160, 9, 20); preview.previewCamera.transform.LookAt(new Vector3(140, -0.5f, 0));
            world.sky = BuildSky(assets, preview.previewCamera.transform);
            // Metadata appears in Edit mode without putting a facilitator view in any player board.
            var source = JObject.Parse(File.ReadAllText("Data/Scenarios/slice_texas.json")); jobSign.Apply((JObject)source["job"]["sign"]);
            Selection.activeGameObject = world.gameObject;
            if (saveScene)
            {
                Directory.CreateDirectory("Assets/LOBOver/Scenes");
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Could not save " + ScenePath);
            }
            AssetDatabase.SaveAssets(); Debug.Log("LOB Over: Site Scene built. Press Play for the slice."); return scene;
        }
        private static void BuildGround(WorldAssets assets)
        {
            // WO-08: 1200 x 200 m terrain. Separate banks leave the 1.2 m trench visible down to its floor.
            var ground = new GameObject("Ground");
            const float length = 1200, width = 200, corridor = WorldUnits.TrenchWidth;
            for (var side = -1; side <= 1; side += 2)
                WorldDrawing.Box(side < 0 ? "FarBank" : "NearBank", ground.transform, new Vector3(500, -1, side * (width + corridor) / 4),
                    new Vector3(length, 2, (width - corridor) / 2), assets.soil);
            WorldDrawing.Box("Subsoil", ground.transform, new Vector3(500, -2.1f, 0), new Vector3(length, 0.2f, width), assets.soil);
            WorldDrawing.Box("BeforeTrench", ground.transform, new Vector3(-50, -1, 0), new Vector3(100, 2, corridor), assets.soil);
            // Initial slice length; stations beyond it are generated when a longer stage loads.
            WorldDrawing.Box("Road", null, new Vector3(460, 0.015f, WorldUnits.RoadZ), new Vector3(980, 0.03f, 8), SiteAssetBuilder.Material("AccessRoad"));
            new GameObject("TrenchCorridor");
        }
        private static GameObject Board(string name, Vector3 position, Vector2 size, WorldAssets assets)
        {
            var root = new GameObject(name); root.transform.position = position;
            WorldDrawing.Box("Face", root.transform, Vector3.zero, new Vector3(size.x, size.y, 0.06f), assets.steel); return root;
        }
        private static GameObject BuildPreviewPrefab()
        {
            var root = new GameObject("DesktopPreview");
            try
            {
                var preview = root.AddComponent<DesktopPreview>();
                var cameraObject = new GameObject("DesktopFlyCamera"); cameraObject.transform.SetParent(root.transform, false);
                preview.previewCamera = cameraObject.AddComponent<Camera>(); preview.previewCamera.nearClipPlane = 0.05f; preview.previewCamera.farClipPlane = 6000;
                preview.previewCamera.fieldOfView = 65; cameraObject.tag = "MainCamera"; cameraObject.AddComponent<AudioListener>();
                return PrefabUtility.SaveAsPrefabAsset(root, SiteAssetBuilder.Prefabs + "DesktopPreview.prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }
        private static SiteSky BuildSky(WorldAssets assets, Transform observer)
        {
            var sky = new GameObject("SiteSky").AddComponent<SiteSky>(); sky.observer = observer;
            var rainObject = new GameObject("Rain"); rainObject.transform.SetParent(sky.transform, false);
            var rain = rainObject.AddComponent<ParticleSystem>(); rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = rain.main; main.loop = true; main.playOnAwake = false; main.startLifetime = 1.2f; main.startSpeed = 24; main.startSize = 0.035f;
            main.maxParticles = 1800; main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = rain.emission; emission.rateOverTime = 1000;
            var shape = rain.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(24, 24, 1);
            rainObject.transform.rotation = Quaternion.Euler(90, 0, 0);
            var renderer = rain.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = assets.rain;
            renderer.renderMode = ParticleSystemRenderMode.Stretch; renderer.lengthScale = 5; renderer.velocityScale = 0.02f;
            sky.rain = rain; rainObject.SetActive(false);
            var clouds = new GameObject("TowersSouth"); clouds.transform.SetParent(sky.transform, false); clouds.transform.localPosition = new Vector3(450, 180, -2000);
            sky.cloudBank = clouds.transform;
            for (var i = 0; i < 9; i++)
            {
                var cloud = new GameObject("Cumulus_" + i); cloud.transform.SetParent(clouds.transform, false);
                cloud.transform.localPosition = new Vector3((i - 4) * 150, i % 3 * 45, 0);
                cloud.AddComponent<MeshFilter>().sharedMesh = CloudMesh(); cloud.AddComponent<MeshRenderer>().sharedMaterial = assets.cloud;
            }
            clouds.SetActive(false);
            var dome = GameObject.CreatePrimitive(PrimitiveType.Sphere); dome.name = "SkyInspectDome";
            dome.transform.SetParent(sky.transform, false); dome.transform.localScale = Vector3.one * 10000;
            var source = dome.GetComponent<MeshFilter>().sharedMesh; var path = SiteAssetBuilder.Art + "SkyDome.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = Object.Instantiate(source); mesh.name = "InwardSkyDome"; var triangles = mesh.triangles;
                for (var i = 0; i < triangles.Length; i += 3) { var a = triangles[i]; triangles[i] = triangles[i + 2]; triangles[i + 2] = a; }
                mesh.triangles = triangles; mesh.RecalculateNormals(); AssetDatabase.CreateAsset(mesh, path);
            }
            Object.DestroyImmediate(dome.GetComponent<Collider>()); Object.DestroyImmediate(dome.GetComponent<Renderer>()); Object.DestroyImmediate(dome.GetComponent<MeshFilter>());
            dome.AddComponent<MeshCollider>().sharedMesh = mesh; InspectTarget.Attach(dome, "sky"); return sky;
        }
        private static Mesh CloudMesh()
        {
            var path = SiteAssetBuilder.Art + "Cumulus.asset"; var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null) return mesh;
            const int edges = 48; var vertices = new Vector3[edges + 1]; var triangles = new int[edges * 3];
            for (var i = 0; i < edges; i++)
            {
                var angle = i * Mathf.PI * 2 / edges; var radius = 1 + 0.12f * Mathf.Cos(angle * 7);
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius * 170, Mathf.Sin(angle) * radius * 70, 0);
                triangles[i * 3] = 0; triangles[i * 3 + 1] = (i + 1) % edges + 1; triangles[i * 3 + 2] = i + 1;
            }
            mesh = new Mesh { name = "Cumulus", vertices = vertices, triangles = triangles }; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path); return mesh;
        }
        [MenuItem("LOB Over/Debug/Screenshot")]
        public static void Screenshot()
        {
            var folder = Path.GetFullPath("Monitor/out/unity"); Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, "site-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".png");
            ScreenCapture.CaptureScreenshot(path); Debug.Log("Screenshot: " + path);
        }
    }
}
