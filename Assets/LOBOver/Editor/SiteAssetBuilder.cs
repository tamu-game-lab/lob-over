using System;
using System.IO;
using System.Linq;
using LOBOver.World;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace LOBOver.Editor
{
    public static class SiteAssetBuilder
    {
        public const string Imported = "Assets/LOBGame/";
        public const string Art = "Assets/LOBOver/Art/";
        public const string Prefabs = "Assets/LOBOver/Prefabs/";
        public const string Equipment = Imported + "_Project/Art/Prefabs/Equipment/";
        public const string Worker = Imported + "_Project/Art/Prefabs/Workers/ProjectWorker.prefab";
        public const string Pipe = Imported + "_Project/Art/ProjectSite/ProjectPipeSegment.prefab";
        public static readonly string[] MachineNames = { "Excavator_Small", "Excavator_Standard", "Excavator_Large", "Crane_Standard", "Crane_Heavy", "Backfill_Small", "Backfill_Standard", "Backfill_Large" };
        public static T Required<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new FileNotFoundException("Required imported asset: " + path);
        public static Material Material(string name) => Required<Material>(Imported + "_Project/Art/ProjectSite/Materials/" + name + ".mat");
        private static AudioClip Audio(string name, string extension = ".ogg") => Required<AudioClip>(Imported + "Audio/Construction/" + name + extension);
        public static WorldAssets Build()
        {
            if (!File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset"))
            {
                TMP_PackageResourceImporter.ImportResources(true, false, false);
                AssetDatabase.Refresh();
            }
            Directory.CreateDirectory(Art); Directory.CreateDirectory(Prefabs); Directory.CreateDirectory(Prefabs + "Imported"); AssetDatabase.Refresh();
            var assets = AssetDatabase.LoadAssetAtPath<WorldAssets>(Art + "WorldAssets.asset");
            if (assets == null) { assets = ScriptableObject.CreateInstance<WorldAssets>(); AssetDatabase.CreateAsset(assets, Art + "WorldAssets.asset"); }
            assets.font = TMP_Settings.defaultFontAsset; assets.soil = Material("Soil"); assets.timber = Material("Timber"); assets.steel = Material("StorageSteel"); assets.orange = Material("SafetyOrange");
            assets.ink = OwnMaterial("Ink", Color.white, false, "Universal Render Pipeline/Particles/Unlit");
            assets.cloud = OwnMaterial("Cloud", new Color(0.26f, 0.28f, 0.31f));
            assets.rain = OwnMaterial("Rain", new Color(0.65f, 0.72f, 0.8f, 0.5f), true);
            assets.wind = Audio("bed_open_wind"); assets.rainfall = Audio("bed_rain_open"); assets.excavation = Audio("exc_work_loop");
            assets.dozer = Audio("dozer_engine_loop"); assets.idle = Audio("machine_idle_loop"); assets.conflict = Audio("conflict_murmur_loop");
            assets.collapse = Audio("collapse_rumble_01", ".wav"); assets.pipeSetdown = Audio("pipe_setdown_01", ".wav");
            assets.worker = CopyModel(Worker, "ProjectWorker", Vector3.zero, 0, true);
            assets.particles = CopyModel(Imported + "_Project/Art/Prefabs/Effects/WorkParticles.prefab", "WorkParticles", Vector3.zero);
            assets.spoil = CopyModel(Imported + "3D_Models/a-pile-of-sand/source/hromada_pisek_01_30k_uw/hromada_pisek_01_30k_uw.FBX", "Spoil", new Vector3(9, 1.5f, 3));
            assets.barrier = CopyModel(Imported + "3D_Models/water-barriers/source/WaterBarriers_RED-WHITE.fbx", "WaterBarrier", new Vector3(2, 1, 0.5f));
            assets.cone = CopyModel(Imported + "3D_Models/trafic-cone/source/vlc.prefab", "Cone", new Vector3(0.45f, 0.7f, 0.45f));
            var catalog = JObject.Parse(File.ReadAllText("Data/catalog.json"));
            assets.machines = MachineNames.Select(name =>
            {
                var key = name.ToLowerInvariant(); var data = catalog["machines"][key];
                return new MachineAsset { crew = (string)data["crew"], plate = (string)data["sizeClass"], label = (string)data["label"],
                    prefab = CopyModel(Equipment + name + ".prefab", name, Vector3.zero, 90, false, true) };
            }).ToArray();
            assets.cellPrefab = BuildCell(assets); assets.crewPrefab = BuildCrew();
            EditorUtility.SetDirty(assets); AssetDatabase.SaveAssets(); return assets;
        }
        public static Material OwnMaterial(string name, Color colour, bool transparent = false, string shaderName = "Universal Render Pipeline/Unlit")
        {
            var path = Art + name + ".mat"; var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find(shaderName) ?? throw new InvalidOperationException("Missing shader " + shaderName);
                material = new Material(shader); AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", colour);
            if (transparent)
            {
                material.SetFloat("_Surface", 1); material.SetFloat("_Blend", 0); material.SetFloat("_ZWrite", 0);
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                material.SetOverrideTag("RenderType", "Transparent"); material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.renderQueue = (int)RenderQueue.Transparent;
            }
            EditorUtility.SetDirty(material); return material;
        }
        public static void StripMissing(GameObject instance)
        {
            foreach (var child in instance.GetComponentsInChildren<Transform>(true)) GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
        }
        private static Bounds BoundsOf(GameObject obj)
        {
            var renderers = obj.GetComponentsInChildren<Renderer>().Where(renderer => !(renderer is ParticleSystemRenderer)).ToArray();
            if (renderers.Length == 0) return new Bounds(obj.transform.position, Vector3.one);
            var bounds = renderers[0].bounds; foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds); return bounds;
        }
        private static GameObject CopyModel(string path, string name, Vector3 fit, float yaw = 0, bool worker = false, bool workingEdge = false)
        {
            var root = new GameObject(name);
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(Required<GameObject>(path));
                model.transform.SetParent(root.transform, false);
                if (PrefabUtility.IsPartOfPrefabInstance(model)) PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                StripMissing(model); model.transform.SetParent(root.transform, false); model.transform.localRotation = Quaternion.Euler(0, yaw, 0);
                if (worker)
                {
                    var animator = model.GetComponentInChildren<Animator>();
                    if (animator != null) { animator.runtimeAnimatorController = Required<RuntimeAnimatorController>(Imported + "_Project/Art/ProjectSite/Animation/ProjectWorker.controller"); animator.applyRootMotion = false; }
                }
                else if (workingEdge)
                {
                    var controllerName = name.StartsWith("Excavator", StringComparison.Ordinal) ? "Excavator" : name.StartsWith("Crane", StringComparison.Ordinal) ? "Crane" : null;
                    foreach (var animator in model.GetComponentsInChildren<Animator>())
                    {
                        animator.applyRootMotion = false;
                        if (animator.runtimeAnimatorController == null && controllerName != null)
                            animator.runtimeAnimatorController = Required<RuntimeAnimatorController>(Imported + "_Project/Animation/" + controllerName + ".controller");
                    }
                    if (!model.GetComponentsInChildren<Animator>().Any(animator => animator.runtimeAnimatorController != null))
                    {
                        // Static imported machines still need a working/still animation. The small
                        // chassis vibration is presentation only; station movement stays on CrewRig.
                        var motion = WorldDrawing.Group("WorkMotion", root.transform);
                        model.transform.SetParent(motion.transform, false);
                        motion.AddComponent<Animator>().runtimeAnimatorController = WorkController();
                    }
                }
                var bounds = BoundsOf(model);
                if (fit != Vector3.zero)
                {
                    model.transform.localScale = Vector3.Scale(model.transform.localScale, new Vector3(fit.x / bounds.size.x, fit.y / bounds.size.y, fit.z / bounds.size.z));
                    bounds = BoundsOf(model);
                }
                model.transform.position -= new Vector3(workingEdge ? bounds.max.x : bounds.center.x, bounds.min.y, bounds.center.z);
                if (model.GetComponentsInChildren<Collider>().Length == 0 && name != "WorkParticles")
                {
                    bounds = BoundsOf(model); var collider = root.AddComponent<BoxCollider>(); collider.center = bounds.center; collider.size = bounds.size;
                }
                return PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "Imported/" + name + ".prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }
        private static CrewRig BuildCrew()
        {
            var root = new GameObject("CrewRig"); root.AddComponent<CrewRig>();
            try { return PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "CrewRig.prefab").GetComponent<CrewRig>(); }
            finally { Object.DestroyImmediate(root); }
        }
        private static RuntimeAnimatorController WorkController()
        {
            var path = Art + "MachineWork.controller"; var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller != null) return controller;
            const float cycleSeconds = 1, vibrationMetres = 0.015f; // Static-machine presentation tuning.
            var clip = new AnimationClip { name = "Cycle" };
            clip.SetCurve("", typeof(Transform), "m_LocalPosition.y", new AnimationCurve(new Keyframe(0, 0), new Keyframe(cycleSeconds / 2, vibrationMetres), new Keyframe(cycleSeconds, 0)));
            var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = true; AnimationUtility.SetAnimationClipSettings(clip, settings);
            AssetDatabase.CreateAsset(clip, Art + "MachineWork.anim");
            controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            var state = controller.AddMotion(clip); controller.layers[0].stateMachine.defaultState = state;
            EditorUtility.SetDirty(controller); return controller;
        }
        private static StationCell BuildCell(WorldAssets assets)
        {
            var root = new GameObject("StationCell"); var cell = root.AddComponent<StationCell>();
            const float length = WorldUnits.CellLength, depth = WorldUnits.TrenchDepth, width = WorldUnits.TrenchWidth;
            try
            {
                cell.surfaceMaterial = Material("Repair_Surface"); cell.openMaterial = Material("Repair_OpenTrench"); cell.pipeMaterial = Material("Repair_PipeBed"); cell.backfillMaterial = Material("Repair_Backfill");
                cell.surface = WorldDrawing.Box("Surface", root.transform, new Vector3(length / 2, -0.05f, 0), new Vector3(length, 0.1f, width), cell.surfaceMaterial);
                cell.floor = WorldDrawing.Box("Floor", root.transform, new Vector3(length / 2, -depth - 0.05f, 0), new Vector3(length, 0.1f, width), cell.openMaterial);
                InspectTarget.Attach(cell.floor, "water");
                cell.walls = WorldDrawing.Group("Walls", root.transform);
                cell.nearWall = WorldDrawing.Box("NearWall", cell.walls.transform, new Vector3(length / 2, -depth / 2, width / 2), new Vector3(length, depth, 0.08f), assets.soil).transform;
                cell.farWall = WorldDrawing.Box("FarWall", cell.walls.transform, new Vector3(length / 2, -depth / 2, -width / 2), new Vector3(length, depth, 0.08f), assets.soil).transform;
                InspectTarget.Attach(cell.walls, "wall");
                cell.pipes = WorldDrawing.Group("Pipes", root.transform);
                var pipePrefab = CopyModel(Pipe, "ProjectPipeSegment", Vector3.zero);
                for (var i = 0; i < 3; i++)
                {
                    var segment = (GameObject)PrefabUtility.InstantiatePrefab(pipePrefab); segment.transform.SetParent(cell.pipes.transform, false);
                    var bounds = BoundsOf(segment); var size = bounds.size;
                    if (size.z > size.x) segment.transform.localRotation = Quaternion.Euler(0, 90, 0);
                    else if (size.y > size.x) segment.transform.localRotation = Quaternion.Euler(0, 0, 90);
                    bounds = BoundsOf(segment);
                    // Three equal segments span each 100 ft cell, retaining the prefab's pipe diameter.
                    var stretch = WorldDrawing.Group("PipeLength", cell.pipes.transform); segment.transform.SetParent(stretch.transform, false);
                    stretch.transform.localScale = new Vector3((length / 3) / bounds.size.x, 1, 1);
                    bounds = BoundsOf(segment);
                    stretch.transform.position += new Vector3(length * (i + 0.5f) / 3 - bounds.center.x, -depth - bounds.min.y, -bounds.center.z);
                    InspectTarget.Attach(stretch, "pipe");
                }
                cell.wetBand = WorldDrawing.Box("SeepBand", root.transform, new Vector3(length / 2, -depth + 0.2f, -width / 2 + 0.05f), new Vector3(length, 0.4f, 0.01f), OwnMaterial("WetSoil", new Color(0.13f, 0.12f, 0.1f)));
                InspectTarget.Attach(cell.wetBand, "water");
                cell.water = WorldDrawing.Box("StandingWater", root.transform, new Vector3(length / 2, -depth + 0.2f, 0), new Vector3(length, 0.008f, width), OwnMaterial("Water", new Color(0.25f, 0.32f, 0.34f, 0.65f), true));
                InspectTarget.Attach(cell.water, "water");
                cell.slump = WorldDrawing.Group("SlumpedWall", root.transform); cell.slump.AddComponent<MeshFilter>().sharedMesh = SlumpMesh();
                cell.slump.AddComponent<MeshRenderer>().sharedMaterial = assets.soil; cell.slump.AddComponent<MeshCollider>().sharedMesh = cell.slump.GetComponent<MeshFilter>().sharedMesh;
                InspectTarget.Attach(cell.slump, "wall");
                cell.box = Protection(root.transform, "TrenchBox", assets.steel, true); cell.shoring = Protection(root.transform, "Shoring", assets.timber, false);
                var post = WorldDrawing.Box("Stake", root.transform, new Vector3(0, 0.55f, 1.2f), new Vector3(0.08f, 1.1f, 0.08f), assets.timber);
                cell.stake = post.transform;
                cell.stationText = WorldDrawing.Text("Station", root.transform, new Vector3(0, 1.3f, 1.25f), new Vector2(1.4f, 0.4f), 0.3f, assets.font);
                cell.stationText.transform.localRotation = Quaternion.Euler(0, 180, 0);
                foreach (var obj in new[] { cell.floor, cell.walls, cell.pipes, cell.wetBand, cell.water, cell.slump, cell.box, cell.shoring }) obj.SetActive(false);
                return PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "StationCell.prefab").GetComponent<StationCell>();
            }
            finally { Object.DestroyImmediate(root); }
        }
        private static GameObject Protection(Transform parent, string name, Material material, bool steel)
        {
            var root = WorldDrawing.Group(name, parent);
            for (var side = -1; side <= 1; side += 2)
                WorldDrawing.Box("Panel", root.transform, new Vector3(WorldUnits.CellLength / 2, -0.9f, side * 0.5f), new Vector3(WorldUnits.CellLength, 1.7f, steel ? 0.06f : 0.1f), material);
            for (var i = 0; i < 3; i++) WorldDrawing.Box("Strut", root.transform, new Vector3((i + 0.5f) * WorldUnits.CellLength / 3, -0.3f, 0), new Vector3(0.1f, 0.1f, 1), material);
            InspectTarget.Attach(root, "protection"); return root;
        }
        private static Mesh SlumpMesh()
        {
            var path = Art + "Slump.asset"; var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null) return mesh;
            var end = WorldUnits.CellLength;
            mesh = new Mesh { name = "Slump" };
            mesh.vertices = new[] { new Vector3(0, -1.8f, -0.6f), new Vector3(0, -0.25f, -0.6f), new Vector3(0, -1.8f, 0.25f),
                new Vector3(end, -1.8f, -0.6f), new Vector3(end, -0.25f, -0.6f), new Vector3(end, -1.8f, 0.25f) };
            mesh.triangles = new[] { 0, 2, 1, 3, 4, 5, 0, 1, 4, 0, 4, 3, 1, 2, 5, 1, 5, 4, 2, 0, 3, 2, 3, 5 };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, path); return mesh;
        }
    }
}
