using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LOBOver.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace LOBOver.Editor
{
    public static class SiteModelBuilder
    {
        private const float ScaleTolerance = 0.25f, WorkerHeight = 1.8f, CabHeight = 3.2f, PipeDiameter = 0.3f; // WO-L4 item 10.
        private const int TreeTriangleLimit = 8000; // WO-L4 item 6, strict upper bound.
        public static Bounds Bounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer)).ToArray();
            if (renderers.Length == 0) throw new InvalidOperationException("Imported model has no renderer: " + root.name);
            var bounds = renderers[0].bounds; foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds); return bounds;
        }
        public static GameObject Copy(GameObject source, string name, float height = 0, bool pipe = false, WorldAssets assets = null)
        {
            if (source == null) throw new FileNotFoundException("Required model " + name);
            var root = new GameObject(name);
            try
            {
                // Always clone the source hierarchy, independently of the destination prefab.
                var model = Object.Instantiate(source); model.name = source.name; model.transform.SetParent(root.transform, false);
                if (PrefabUtility.IsPartOfPrefabInstance(model)) PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                SiteAssetBuilder.StripMissing(model);
                foreach (var collider in root.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                foreach (var light in root.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(light);
                GameObject distantRig = null;
                if (name.StartsWith("Excavator", StringComparison.Ordinal))
                {
                    // WO-L4 rejection 3: Mesh1552 duplicates the complete rigged machine.
                    foreach (var duplicate in model.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Mesh1552").ToArray())
                        Object.DestroyImmediate(duplicate.gameObject);
                    distantRig = WorldDrawing.Group("DistantRig", root.transform);
                    foreach (var box in model.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Body" || t.name == "Boom").ToArray())
                    { box.SetParent(distantRig.transform, true); box.gameObject.SetActive(true); }
                }
                var bounds = Bounds(model);
                if ((name.Contains("Fence") || name.Contains("Jersey") || name.Contains("Barrier")) && bounds.size.z > bounds.size.x)
                {
                    // The OBJ modules are authored along Z; the road and fence run along X.
                    model.transform.localRotation *= Quaternion.Euler(0, 90, 0); bounds = Bounds(model);
                }
                if (pipe)
                {
                    if (bounds.size.z > bounds.size.x) model.transform.localRotation *= Quaternion.Euler(0, 90, 0);
                    else if (bounds.size.y > bounds.size.x) model.transform.localRotation *= Quaternion.Euler(0, 0, 90);
                    bounds = Bounds(model); var diameter = Mathf.Max(bounds.size.y, bounds.size.z);
                    ScaleLog(name + " outside diameter", diameter, PipeDiameter); model.transform.localScale *= PipeDiameter / diameter;
                }
                else if (height > 0)
                {
                    var measured = bounds.size.y;
                    if (name.StartsWith("Excavator", StringComparison.Ordinal))
                    {
                        var cab = model.GetComponentsInChildren<Renderer>(true).Where(r => r.name.IndexOf("cab", StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
                        if (cab.Length > 0) measured = cab.Max(r => r.bounds.max.y) - bounds.min.y;
                    }
                    ScaleLog(name + " height", measured, height); model.transform.localScale *= height / Mathf.Max(measured, 0.001f);
                }
                bounds = Bounds(model);
                model.transform.position -= new Vector3(name.StartsWith("Excavator", StringComparison.Ordinal) || name.StartsWith("Crane", StringComparison.Ordinal)
                    || name.StartsWith("Backfill", StringComparison.Ordinal) ? bounds.max.x : bounds.center.x, bounds.min.y, bounds.center.z);
                if (distantRig != null) ExcavatorLod(root, model, distantRig);
                SiteMaterialBuilder.Convert(root); Maps(root, AssetDatabase.GetAssetPath(source), assets);
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                { renderer.receiveShadows = true; renderer.shadowCastingMode = ShadowCastingMode.On; }
                return SaveIfChanged(root, SiteAssetBuilder.Converted + "Play_" + name + ".prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }
        private static void ExcavatorLod(GameObject root, GameObject model, GameObject distantRig)
        {
            var target = Bounds(model); var source = Bounds(distantRig);
            distantRig.transform.localScale = new Vector3(target.size.x / source.size.x, target.size.y / source.size.y, target.size.z / source.size.z);
            distantRig.transform.position += target.center - Bounds(distantRig).center;
            foreach (var old in model.GetComponentsInChildren<LODGroup>(true)) Object.DestroyImmediate(old);
            var group = root.AddComponent<LODGroup>();
            var full = model.GetComponentsInChildren<Renderer>(true); var far = distantRig.GetComponentsInChildren<Renderer>(true);
            // Native Editor LOD at the preview's 60-degree FOV. CrewRig forces the same
            // 60 m boundary from the head at runtime, independent of headset FOV/bias.
            var size = Mathf.Max(target.size.x, Mathf.Max(target.size.y, target.size.z));
            var threshold = Mathf.Clamp01(size / (2 * CrewRig.MachineDetailMetres * Mathf.Tan(30 * Mathf.Deg2Rad)));
            group.SetLODs(new[] { new LOD(threshold, full), new LOD(0, far) });
            group.localReferencePoint = Vector3.zero; group.size = size; group.fadeMode = LODFadeMode.None;
        }
        public static GameObject SaveIfChanged(GameObject root, string path)
        {
            // Unity imports a prefab root under its filename. Use that name on the first
            // build too, so the next comparison does not force another ambiguous save.
            root.name = Path.GetFileNameWithoutExtension(path);
            var previous = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            // Saving an equivalent unpacked hierarchy can reassign duplicate-named FBX
            // children to different local IDs. Compare content before invoking the saver.
            if (previous != null && SameContent(root, previous)) return previous;
            return PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        public static bool SameContent(GameObject candidate, GameObject previous)
        {
            var objects = HierarchyObjects(candidate.transform); var otherObjects = HierarchyObjects(previous.transform);
            if (objects.Count != otherObjects.Count || objects.Values.Any(o => o == null) || otherObjects.Values.Any(o => o == null)) return false;
            var paths = objects.ToDictionary(p => p.Value, p => p.Key);
            var otherPaths = otherObjects.ToDictionary(p => p.Value, p => p.Key);
            foreach (var pair in objects)
            {
                if (!otherObjects.TryGetValue(pair.Key, out var otherObject) || pair.Value.GetType() != otherObject.GetType()) return false;
                if (pair.Value is GameObject gameObject)
                {
                    var components = SavedComponents(gameObject); var otherComponents = SavedComponents((GameObject)otherObject);
                    if (components.Length != otherComponents.Length) return false;
                    for (var i = 0; i < components.Length; i++)
                        if (!SameReference(components[i], otherComponents[i], paths, otherPaths)) return false;
                }
                using var a = new SerializedObject(pair.Value); using var b = new SerializedObject(otherObject);
                var property = a.GetIterator(); var enterChildren = true;
                while (property.Next(enterChildren))
                {
                    enterChildren = false;
                    // Prefab linkage and temporary hide flags describe storage, not content.
                    if (property.propertyPath == "m_CorrespondingSourceObject" || property.propertyPath == "m_PrefabInstance"
                        || property.propertyPath == "m_PrefabAsset" || property.propertyPath == "m_ObjectHideFlags") continue;
                    // TMP's transient MeshFilter never enters the prefab's serialized list.
                    if (pair.Value is GameObject && property.propertyPath == "m_Component") continue;
                    // A root's position among unrelated scene objects is not prefab content.
                    if (pair.Value == candidate.transform && (property.propertyPath == "m_RootOrder" || property.propertyPath == "m_Father")) continue;
                    var other = b.FindProperty(property.propertyPath);
                    if (other == null || property.propertyType != other.propertyType) return false;
                    if (property.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        if (!SameReference(property.objectReferenceValue, other.objectReferenceValue, paths, otherPaths)) return false;
                    }
                    // Descend only into containers, never into a reference's internal instance ID.
                    else if (property.propertyType == SerializedPropertyType.Generic) enterChildren = true;
                    else if (!SerializedProperty.DataEquals(property, other)) return false;
                }
            }
            return true;
        }
        private static Dictionary<string, Object> HierarchyObjects(Transform root)
        {
            var objects = new Dictionary<string, Object>(StringComparer.Ordinal);
            void Visit(Transform transform, string path)
            {
                objects.Add(path + "/GameObject", transform.gameObject);
                var components = SavedComponents(transform.gameObject);
                for (var i = 0; i < components.Length; i++)
                    objects.Add(path + "/Component[" + i + "]:" + components[i]?.GetType().FullName, components[i]);
                // Sibling indices disambiguate the repeated names in imported models and bones.
                for (var i = 0; i < transform.childCount; i++) Visit(transform.GetChild(i), path + "/Child[" + i + "]");
            }
            Visit(root, "Root"); return objects;
        }
        private static Component[] SavedComponents(GameObject obj) => obj.GetComponents<Component>()
            .Where(c => c == null || (c.hideFlags & HideFlags.DontSave) == 0).ToArray();
        private static bool SameReference(Object a, Object b, Dictionary<Object, string> paths, Dictionary<Object, string> otherPaths)
        {
            if (a == null || b == null) return a == null && b == null;
            var internalA = paths.TryGetValue(a, out var path); var internalB = otherPaths.TryGetValue(b, out var otherPath);
            if (internalA || internalB) return internalA && internalB && path == otherPath;
            // Shared assets have persistent GUID/local IDs; scene instance IDs and bone
            // object identity are never used as the value of a serialized reference.
            return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(a, out string guid, out long localId)
                && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(b, out string otherGuid, out long otherId)
                && guid == otherGuid && localId == otherId;
        }
        private static GameObject BuiltModel(string name, Mesh mesh, Material material)
        {
            var root = new GameObject(name);
            try
            {
                root.AddComponent<MeshFilter>().sharedMesh = mesh;
                root.AddComponent<MeshRenderer>().sharedMaterial = material;
                return SaveIfChanged(root, SiteAssetBuilder.Converted + "Play_" + name + ".prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }
        private static GameObject Pipe(WorldAssets assets)
        {
            var material = SiteMaterialBuilder.Get(SiteMaterialBuilder.Folder + "LaidPipe.mat", "LOBOver/SiteLit");
            material.shaderKeywords = Array.Empty<string>(); material.SetFloat("_World", 0); material.SetFloat("_Splat", 0);
            var root = new GameObject("PipeMaps");
            try
            {
                root.AddComponent<MeshRenderer>().sharedMaterial = material;
                Maps(root, SiteAssetBuilder.Imported + "3D_Models/pipe/source/PIPE.fbx", assets);
            }
            finally { Object.DestroyImmediate(root); }
            return BuiltModel("Pipe", SiteGeometryBuilder.Pipe(), material);
        }
        private static void ScaleLog(string name, float measured, float target)
        {
            if (Mathf.Abs(measured / target - 1) > ScaleTolerance)
                Debug.LogWarning($"WO-L4 scale: {name} was {measured:F3} m; scaled to {target:F3} m (over 25% off).");
        }
        public static GameObject Model(string path, string name, float height, WorldAssets assets, bool optional = false, bool pipe = false)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SiteAssetBuilder.Imported + path);
            if (source == null && optional) { Debug.LogWarning("WO-L4 optional import missing: " + path + "; rerun Play Scene after import."); return null; }
            return Copy(source, name, height, pipe, assets);
        }
        public static void Populate(WorldAssets assets, WorldAssets legacy)
        {
            assets.worker = Copy(legacy.worker, "Worker", WorkerHeight, assets: assets);
            assets.machines = legacy.machines.Select(m => new MachineAsset { crew = m.crew, label = m.label, plate = m.plate,
                prefab = Copy(m.prefab, m.prefab.name, m.crew == "exc" ? CabHeight : 0, assets: assets) }).ToArray();
            assets.pipe = Pipe(assets);
            assets.spoil = BuiltModel("HeroSpoil", SiteGeometryBuilder.HeroPile(), assets.spoilSurface);
            assets.cone = Copy(legacy.cone, "Cone", 0.7f, assets: assets);
            assets.barrier = Copy(legacy.barrier, "WaterBarrier", 1, assets: assets);
            const string props = "3D_Models/Construction Assets/Traffic Props/";
            assets.fence = Model(props + "fence_long/fence_long.obj", "Fence", 1.8f, assets);
            assets.jersey = Model(props + "jersey_barrier/jersey_barrier.obj", "Jersey", 0.9f, assets);
            // Convert every material in the four additions, including FBX subassets, without
            // changing importers or remaps in the read-only LOBGame copies.
            foreach (var folder in new[] { "Tree", "sophiaImports", "traffic-props", "construction-site-barrier" })
            {
                var path = SiteAssetBuilder.Imported + "3D_Models/" + folder;
                if (!AssetDatabase.IsValidFolder(path)) { Debug.LogWarning("WO-L4 optional import missing: " + path); continue; }
                foreach (var guid in AssetDatabase.FindAssets("", new[] { path }).OrderBy(g => g, StringComparer.Ordinal))
                    foreach (var material in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)).OfType<Material>()) SiteMaterialBuilder.Copy(material);
            }
            var newFence = Model("3D_Models/traffic-props/source/Traffic Props/Traffic Props/fence_long/fence_long.obj", "AddedFence", 1.8f, assets, true);
            if (newFence != null) assets.fence = newFence;
            var newBarrier = Model("3D_Models/construction-site-barrier/source/blocker.fbx", "ConstructionBarrier", 1, assets, true);
            if (newBarrier != null) assets.barrier = newBarrier;
            assets.tree = Tree(assets);
        }
        public static int Triangles(GameObject model) => model.GetComponentsInChildren<MeshFilter>(true).Sum(f => f.sharedMesh == null ? 0 : Enumerable.Range(0, f.sharedMesh.subMeshCount).Sum(i => (int)f.sharedMesh.GetIndexCount(i) / 3))
            + model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Sum(r => r.sharedMesh == null ? 0 : r.sharedMesh.triangles.Length / 3);
        private static GameObject Tree(WorldAssets assets)
        {
            var paths = new[] { "Tree 2/fast-urban-tree/source/Fast-tree.fbx", "Tree 2/beech/beech.fbx", "Tree1/tree/source/TreeGenerator.fbx" };
            var candidate = paths.Select(p => AssetDatabase.LoadAssetAtPath<GameObject>(SiteAssetBuilder.Imported + "3D_Models/Tree/" + p))
                .Where(m => m != null && Triangles(m) > 0 && Triangles(m) < TreeTriangleLimit).OrderBy(Triangles).ThenBy(m => m.name, StringComparer.Ordinal).FirstOrDefault();
            if (candidate == null) { Debug.LogWarning("WO-L4: no imported tree below 8,000 triangles; tree line omitted until import."); return null; }
            Debug.Log("WO-L4 tree: " + candidate.name + ", " + Triangles(candidate) + " triangles.");
            // Level 1's trees are all 80..120 m from the road, beyond the 60 m detail
            // boundary. The full beech survives only as this builder's bake source.
            var source = Copy(candidate, "TreeBakeSource", assets: assets);
            return SiteTreeBuilder.Build(source);
        }
        private static void Maps(GameObject root, string path, WorldAssets assets)
        {
            if (assets == null) return;
            if (path.Contains("a-pile-of-sand"))
            {
                // The hero keeps its scan UVs; only the procedural windrow uses world tiling.
                var scan = SiteMaterialBuilder.Get(SiteMaterialBuilder.Folder + "HeroSpoil.mat", "LOBOver/SiteLit");
                scan.CopyPropertiesFromMaterial(assets.spoilSurface); scan.DisableKeyword("_WORLD"); scan.SetFloat("_World", 0);
                foreach (var r in root.GetComponentsInChildren<Renderer>()) r.sharedMaterials = r.sharedMaterials.Select(_ => scan).ToArray();
                return;
            }
            var folder = Path.GetDirectoryName(path).Replace('\\', '/');
            if (path.Contains("/pipe/")) folder = SiteAssetBuilder.Imported + "3D_Models/pipe/textures";
            if (path.Contains("fast-urban-tree")) folder = SiteAssetBuilder.Imported + "3D_Models/Tree/Tree 2/fast-urban-tree/textures";
            if (path.Contains("Tree1/tree")) folder = SiteAssetBuilder.Imported + "3D_Models/Tree/Tree1/tree/textures";
            if (path.Contains("/beech/"))
            {
                // The FBX 6.1 file binds textures to Tree_0 (bark) and Tree_1 (leaf),
                // without named Material nodes. Keep those two surfaces distinct on import.
                foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                {
                    var leaf = renderer.name.Contains("Tree_1");
                    var material = SiteMaterialBuilder.Get(SiteMaterialBuilder.Folder + (leaf ? "BeechLeaf.mat" : "BeechBark.mat"), "LOBOver/SiteLit");
                    if (renderer.sharedMaterial != null) material.CopyPropertiesFromMaterial(renderer.sharedMaterial);
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => material).ToArray();
                }
            }
            foreach (var material in root.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials).Distinct())
            {
                var albedo = SiteMaterialBuilder.Texture(folder, path.Contains("fast-urban-tree") ? "Fastcolor" : "BaseColor") ?? SiteMaterialBuilder.Texture(folder, "Albedo");
                var normal = SiteMaterialBuilder.Texture(folder, "Normal");
                if (path.Contains("/beech/"))
                {
                    var leaf = material.name.IndexOf("leaf", StringComparison.OrdinalIgnoreCase) >= 0;
                    albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + (leaf ? "/beech leaf.png" : "/bark01.png"));
                    normal = SiteMaterialBuilder.Texture(folder, leaf ? "leaf_Normal" : "bark01_normal");
                }
                if (albedo != null) { material.SetTexture("_BaseMap", albedo); material.SetColor("_BaseColor", Color.white); }
                if (normal != null) material.SetTexture("_BumpMap", SiteTextureBuilder.LinearCopy(normal, true));
                var occlusion = SiteMaterialBuilder.Texture(folder, "Occlusion");
                if (occlusion != null)
                {
                    var map = SiteTextureBuilder.LinearCopy(occlusion, false);
                    if (material.GetTexture("_OcclusionMap") != map) material.SetFloat("_OcclusionFromMask", 0);
                    material.SetTexture("_OcclusionMap", map);
                }
                SiteMaterialBuilder.PackOcclusion(material);
                if (path.Contains("/pipe/"))
                {
                    var metallic = SiteTextureBuilder.LinearCopy(SiteMaterialBuilder.Texture(folder, "Metallic"), false);
                    var roughness = SiteTextureBuilder.LinearCopy(SiteMaterialBuilder.Texture(folder, "Roughness"), false);
                    if (metallic != null && roughness != null)
                    { material.SetTexture("_MetallicGlossMap", SiteTextureBuilder.MetallicRoughness(metallic, roughness)); material.SetFloat("_Smoothness", 1); material.EnableKeyword("_METALLICSPECGLOSSMAP"); }
                }
                if (path.Contains("/Tree/"))
                {
                    var leaf = material.name.IndexOf("leaf", StringComparison.OrdinalIgnoreCase) >= 0;
                    var alpha = path.Contains("/beech/") && !leaf ? null : SiteMaterialBuilder.Texture(folder, path.Contains("fast-urban-tree") ? "Fastmask" : "leaf_alpha");
                    material.SetTexture("_AlphaMap", SiteTextureBuilder.LinearCopy(alpha, false)); material.SetFloat("_UseAlphaMap", alpha == null ? 0 : 1);
                    if (alpha != null && material.GetTexture("_BaseMap") is Texture2D color)
                        material.SetTexture("_BaseMap", SiteTextureBuilder.PackOpacity(color, SiteTextureBuilder.LinearCopy(alpha, false)));
                    material.EnableKeyword("_ALPHATEST_ON"); material.SetFloat("_AlphaClip", 1); material.SetFloat("_Cull", 0);
                }
                EditorUtility.SetDirty(material);
            }
        }
    }
}
