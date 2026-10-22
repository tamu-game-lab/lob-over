using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using LOBOver.Editor;
using LOBOver.Sim;
using LOBOver.World;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace LOBOver.Tests.Editor
{
    public sealed class SitePresentationTests
    {
        [Test]
        public void PlayRebuildHasIdenticalGeometryMaterialsAndArtBytes()
        {
            var scene = PlaySceneBuilder.Build(false);
            var first = Hierarchy(scene.GetRootGameObjects()); var files = ArtHashes();
            PlaySceneBuilder.Build(false);
            var secondFiles = ArtHashes();
            CollectionAssert.AreEqual(first, Hierarchy(scene.GetRootGameObjects()), "Owned hierarchy changed on second build");
            CollectionAssert.AreEquivalent(files.Keys, secondFiles.Keys, "Second build generated extra art");
            foreach (var pair in files) Assert.That(secondFiles[pair.Key], Is.EqualTo(pair.Value), pair.Key);
        }
        private static string[] Hierarchy(IEnumerable<GameObject> roots)
        {
            string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
            return roots.Where(r => r.name.StartsWith("[LOBOver]", StringComparison.Ordinal)).SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .Select(t => PathOf(t) + "|" + t.localPosition.ToString("R") + "|" + t.localRotation.ToString("R") + "|" + t.localScale.ToString("R")
                    + "|" + t.gameObject.activeSelf + "|" + string.Join(",", t.GetComponents<Component>().Select(c => c == null ? "missing" : c.GetType().FullName))
                    + "|" + string.Join(",", t.GetComponents<Renderer>().SelectMany(r => r.sharedMaterials).Select(m => AssetDatabase.GetAssetPath(m))))
                .OrderBy(s => s, StringComparer.Ordinal).ToArray();
        }
        private static Dictionary<string, string> ArtHashes()
        {
            using var hash = SHA256.Create();
            return Directory.GetFiles(SiteAssetBuilder.Art, "*", SearchOption.AllDirectories)
                .Where(p => !p.EndsWith(".meta", StringComparison.Ordinal))
                .ToDictionary(p => p, p => Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes(p))));
        }
        [Test]
        public void EveryRuntimeSiteRendererUsesTexturedApprovedShader()
        {
            var scene = PlaySceneBuilder.Build(false);
            var site = scene.GetRootGameObjects().Single(r => r.name == "[LOBOver] Site");
            var world = site.GetComponentInChildren<SiteWorld>(true); Populate(world);
            Assert.That(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Light>(true)).Count(l => l.enabled), Is.EqualTo(1));
            var shaders = new[] { "LOBOver/SiteLit", "LOBOver/SiteSky", "Universal Render Pipeline/Particles/Unlit" };
            foreach (var renderer in site.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials)
                {
                    Assert.That(material, Is.Not.Null, renderer.name); Assert.That(material.shader, Is.Not.Null, material.name);
                    var lettering = renderer.GetComponent<TMPro.TextMeshPro>() is not null && material.shader.name.StartsWith("TextMeshPro/", StringComparison.Ordinal);
                    if (!lettering) Assert.That(shaders, Does.Contain(material.shader.name), renderer.name + "/" + material.name);
                    if (material.shader.name != "LOBOver/SiteSky") Assert.That(material.mainTexture, Is.Not.Null, renderer.name + "/" + material.name);
                    Assert.That(ShaderUtil.GetShaderMessages(material.shader).Where(m => m.severity.ToString() == "Error"), Is.Empty, material.shader.name);
                }
            Assert.That(site.GetComponentsInChildren<Collider>(true).Where(c => c.enabled), Is.Empty);
            Assert.That(world.assets.tree == null || SiteModelBuilder.Triangles(world.assets.tree) < 8000, Is.True);
            foreach (var crew in site.GetComponentsInChildren<CrewRig>(true))
                Assert.That(crew.GetComponentsInChildren<TMPro.TMP_Text>(true), Is.Empty, "Detailed crews must not have floating labels");
        }
        [Test]
        public void GroundAndPhysicalDetailsMatchTheSpecifiedDimensions()
        {
            var a = SitePresentationBuilder.BuildAssets();
            Assert.That(a.groundMesh.triangles.Length / 3, Is.LessThan(60000));
            Assert.That(a.groundMesh.bounds.size.x, Is.EqualTo(1200).Within(0.01));
            Assert.That(a.groundMesh.bounds.size.z, Is.EqualTo(200).Within(0.01));
            Assert.That(a.groundMesh.vertices.Max(v => v.y) - a.groundMesh.vertices.Min(v => v.y), Is.GreaterThan(0.1f));
            foreach (var vertex in a.groundMesh.vertices.Where(v => Mathf.Abs(v.z) > 12)) Assert.That(Mathf.Abs(vertex.y), Is.LessThanOrEqualTo(0.6f));
            Assert.That(SiteGeometryBuilder.Height(100, WorldUnits.RoadZ + 0.85f), Is.LessThan(SiteGeometryBuilder.Height(100, WorldUnits.RoadZ)));
            foreach (var c in a.groundMesh.colors) Assert.That(c.r + c.g + c.b, Is.EqualTo(1).Within(0.001));
            Assert.That(a.wallNearMesh.bounds.size.y, Is.EqualTo(1.8f).Within(0.001));
            Assert.That(a.stripMesh.bounds.size.z, Is.EqualTo(1.2f).Within(0.001));
            Assert.That(PrefabSize(a.worker).y, Is.EqualTo(1.8f).Within(0.01));
            var pipe = PrefabSize(a.pipe);
            Assert.That(Mathf.Max(pipe.y, pipe.z), Is.EqualTo(0.3f).Within(0.01));
            Assert.That(a.cellPrefab.water.transform.localPosition.y, Is.EqualTo(-1.5f).Within(0.001));
            Assert.That(a.cellPrefab.pipes.GetComponentsInChildren<MeshRenderer>(true).Length, Is.GreaterThan(1));
            Assert.That(SiteModelBuilder.Triangles(a.cellPrefab.pipes), Is.LessThanOrEqualTo(400));
            Assert.That(SiteModelBuilder.Triangles(a.spoil), Is.LessThanOrEqualTo(4000));
            Assert.That(a.spoil.GetComponentInChildren<Renderer>().sharedMaterial, Is.EqualTo(a.spoilSurface));
            var spoilColour = a.spoilSurface.GetColor("_BaseColor"); var expectedSpoil = new Color(0.55f, 0.45f, 0.35f);
            for (var channel = 0; channel < 4; channel++) Assert.That(spoilColour[channel], Is.EqualTo(expectedSpoil[channel]).Within(0.01));
            Assert.That(a.spoilSurface.GetTexture("_BumpMap"), Is.Not.Null);
            Assert.That(a.spoilSurface.GetTexture("_OcclusionMap"), Is.Not.Null);
            Assert.That(a.cellPrefab.debugStateChanges, Is.False);
            Assert.That(a.cellPrefab.stationText.fontSharedMaterial.shader.name, Is.EqualTo("LOBOver/SiteLit"));
            Assert.That(a.cellPrefab.stationText.fontSharedMaterial.IsKeywordEnabled("_SITE_TEXT"), Is.True);
            Assert.That(a.cellPrefab.stationText.fontSharedMaterial.GetTexture("_MainTex"), Is.EqualTo(a.font.atlasTexture));
            Assert.That(a.cellPrefab.stationText.fontSharedMaterial.mainTexture, Is.EqualTo(a.font.atlasTexture));
            // Rejection 3: maps come from tileable terrain layers, never the trench UV atlas.
            foreach (var material in new[] { a.trench, a.wetSoil, a.bedding })
            {
                var layer = material == a.bedding ? a.road : a.soil;
                foreach (var property in new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap" })
                    Assert.That(material.GetTexture(property), Is.EqualTo(layer.GetTexture(property)), material.name + "/" + property);
                Assert.That(material.IsKeywordEnabled("_WORLD"), Is.True);
                Assert.That(material.GetFloat("_TileMetres"), Is.EqualTo(1));
            }
            foreach (var pair in new[] { (a.trench, new Color(0.80f, 0.75f, 0.70f)), (a.wetSoil, new Color(0.45f, 0.42f, 0.40f)) })
                for (var channel = 0; channel < 4; channel++) Assert.That(pair.Item1.GetColor("_BaseColor")[channel], Is.EqualTo(pair.Item2[channel]).Within(0.01));
            Assert.That(a.cellPrefab.pipeMaterial, Is.EqualTo(a.trench));
            Assert.That(SiteModelBuilder.Triangles(a.tree), Is.EqualTo(4));
            var treeMaterial = a.tree.GetComponent<Renderer>().sharedMaterial;
            Assert.That(treeMaterial.IsKeywordEnabled("_ALPHATEST_ON") && treeMaterial.enableInstancing, Is.True);
            Assert.That(treeMaterial.mainTexture.width, Is.EqualTo(512)); Assert.That(treeMaterial.mainTexture.height, Is.EqualTo(512));
            Assert.That(a.tree.GetComponent<Renderer>().shadowCastingMode, Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off));
            foreach (var machine in a.machines.Where(m => m.crew == "exc"))
            {
                Assert.That(machine.prefab.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Mesh1552"), Is.False);
                var lods = machine.prefab.GetComponent<LODGroup>().GetLODs();
                Assert.That(lods.Length, Is.EqualTo(2));
                Assert.That(lods[0].renderers.Sum(RendererTriangles), Is.LessThan(90000));
                CollectionAssert.AreEquivalent(new[] { "Body", "Boom" }, lods[1].renderers.Select(r => r.name));
                Assert.That(lods[1].renderers.Sum(RendererTriangles), Is.EqualTo(24));
            }
            AssertOpenCut(a);
        }
        private static int RendererTriangles(Renderer renderer)
        {
            var mesh = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh : renderer.GetComponent<MeshFilter>().sharedMesh;
            return Enumerable.Range(0, mesh.subMeshCount).Sum(i => (int)mesh.GetIndexCount(i) / 3);
        }
        [Test]
        public void CrewShadowsFollowTheHeadDistanceAndRestoreAfterAMachineChange()
        {
            var assets = SitePresentationBuilder.BuildAssets(); var parent = new GameObject("DistanceTest");
            var head = new GameObject("Head");
            try
            {
                var crew = Object.Instantiate(assets.crewPrefab, parent.transform); crew.Initialize("exc", assets);
                foreach (var machine in assets.machines.Where(m => m.crew == "exc"))
                {
                    crew.Apply(new CrewView { id = "exc", machinePlate = machine.plate, status = "working", stationFt = 100 });
                    foreach (var distance in new[] { 30f, 30.01f, 60.01f, 0f })
                    {
                        head.transform.position = crew.transform.position + Vector3.right * distance;
                        crew.RefreshDistancePresentation(head.transform);
                        var expected = distance <= 30 ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                        foreach (var renderer in crew.GetComponentsInChildren<Renderer>(true))
                            if (!(renderer is ParticleSystemRenderer)) Assert.That(renderer.shadowCastingMode, Is.EqualTo(expected), renderer.name);
                    }
                }
            }
            finally { Object.DestroyImmediate(parent); Object.DestroyImmediate(head); }
        }
        private static void AssertOpenCut(WorldAssets assets)
        {
            var vertices = assets.groundMesh.vertices; var indices = assets.groundMesh.triangles;
            const float tolerance = 0.0001f;
            for (var i = 0; i < indices.Length; i += 3)
            {
                var triangle = new[] { vertices[indices[i]], vertices[indices[i + 1]], vertices[indices[i + 2]] };
                if (triangle.Max(v => v.x) <= 0) continue;
                Assert.That(triangle.Min(v => v.z) >= WorldUnits.TrenchWidth / 2 - tolerance
                    || triangle.Max(v => v.z) <= -WorldUnits.TrenchWidth / 2 + tolerance, Is.True, "Ground bridges the trench footprint");
            }
            foreach (var pair in new[] { (assets.wallNearMesh, 1), (assets.wallFarMesh, -1) })
            {
                Assert.That(pair.Item1.bounds.min.y, Is.EqualTo(-WorldUnits.TrenchDepth).Within(tolerance));
                Assert.That(pair.Item1.bounds.max.y, Is.EqualTo(0).Within(tolerance));
                foreach (var normal in pair.Item1.normals) Assert.That(normal.z * pair.Item2, Is.LessThan(0), "Wall must face into the cut");
            }
            var cell = Object.Instantiate(assets.cellPrefab);
            try
            {
                cell.Initialize(500, assets);
                foreach (var state in new[] { "undug", "open", "piped", "backfilled", "open" })
                {
                    cell.Apply(new TrenchView { state = state, wall = "stable", water = "dry", protection = "none" });
                    var dug = state == "open" || state == "piped";
                    Assert.That(cell.surface.activeSelf, Is.EqualTo(!dug), state);
                    Assert.That(cell.floor.activeSelf && cell.walls.activeSelf, Is.EqualTo(dug), state);
                    Assert.That(cell.pipes.activeSelf, Is.EqualTo(state == "piped"), state);
                    Assert.That(cell.nearWall.GetComponent<MeshFilter>().sharedMesh, Is.EqualTo(assets.wallNearMesh));
                    Assert.That(cell.farWall.GetComponent<MeshFilter>().sharedMesh, Is.EqualTo(assets.wallFarMesh));
                }
            }
            finally { Object.DestroyImmediate(cell.gameObject); }
        }
        [Test]
        public void PrefabComparisonUsesContentAndRemapsInternalReferences()
        {
            var a = new GameObject("Model"); var b = new GameObject("Model");
            try
            {
                foreach (var root in new[] { a, b })
                {
                    // Repeated names reproduce the imported machine's ambiguous local IDs.
                    var first = new GameObject("Part"); first.transform.SetParent(root.transform, false);
                    var second = new GameObject("Part"); second.transform.SetParent(root.transform, false);
                    var renderer = root.AddComponent<SkinnedMeshRenderer>(); renderer.bones = new[] { first.transform, second.transform };
                }
                Assert.That(SiteModelBuilder.SameContent(a, b), Is.True);
                b.transform.GetChild(1).localPosition = Vector3.up;
                Assert.That(SiteModelBuilder.SameContent(a, b), Is.False);
                b.transform.GetChild(1).localPosition = Vector3.zero;
                b.GetComponent<SkinnedMeshRenderer>().bones = new[] { b.transform.GetChild(1), b.transform.GetChild(0) };
                Assert.That(SiteModelBuilder.SameContent(a, b), Is.False, "Bone references are content, not disposable IDs");
                b.GetComponent<SkinnedMeshRenderer>().bones = new[] { b.transform.GetChild(0), b.transform.GetChild(1) };
                Assert.That(SiteModelBuilder.SameContent(a, b), Is.True, "Restoring the bone paths restores equivalent content");
                // Reproduce TMP's unsaved filter between two serialized components.
                var transient = a.AddComponent<MeshFilter>(); transient.hideFlags = HideFlags.HideInInspector | HideFlags.HideAndDontSave;
                a.AddComponent<BoxCollider>(); b.AddComponent<BoxCollider>();
                Assert.That(SiteModelBuilder.SameContent(a, b), Is.True, "DontSave components must not shift serialized component paths");
                Assert.That(SiteModelBuilder.SameContent(b, a), Is.True, "Comparison must be symmetric");
                transient.hideFlags = HideFlags.None;
                Assert.That(SiteModelBuilder.SameContent(a, b), Is.False, "A saved component is a real content change");
            }
            finally { Object.DestroyImmediate(a); Object.DestroyImmediate(b); }
        }
        [Test]
        public void SkyRejectsNonDaylightNamesAndAlignsTheLight()
        {
            foreach (var name in new[] { "AllSky_Overcast4_Low", "NIGHT", "dusk", "Epic_BlueSunset", "Space_AnotherPlanet" })
                Assert.That(SitePresentationBuilder.DaylightSkyName(name), Is.False, name);
            Assert.That(SitePresentationBuilder.DaylightSkyName("ClearDay"), Is.True);
            Assert.That(SitePresentationBuilder.ElevatedCubemapSun(Vector3.zero), Is.False);
            Assert.That(SitePresentationBuilder.ElevatedCubemapSun(new Vector3(1, 0.01f, 0)), Is.False, "Horizon sun must use the procedural sky");
            Assert.That(SitePresentationBuilder.ElevatedCubemapSun(new Vector3(Mathf.Sqrt(3), 1, 0)), Is.False, "Exactly 30 degrees is not above 30");
            Assert.That(SitePresentationBuilder.ElevatedCubemapSun(new Vector3(1, 1, 0)), Is.True);
            var scene = PlaySceneBuilder.Build(false);
            var sun = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Light>()).Single(l => l.enabled);
            var sky = scene.GetRootGameObjects().Single(r => r.name == "[LOBOver] Site").GetComponentInChildren<SiteWorld>(true).assets.skyMaterial;
            if (sky.GetFloat("_UseCube") > 0.5f)
            {
                Assert.That(SitePresentationBuilder.DaylightSkyName(sky.GetTexture("_Cube").name), Is.True);
                Vector3 direction = sky.GetVector("_SunDirection");
                Assert.That(SitePresentationBuilder.ElevatedCubemapSun(direction), Is.True);
                Assert.That(Vector3.Dot(-sun.transform.forward, direction.normalized), Is.GreaterThan(0.999f));
            }
            else Assert.That(sun.transform.eulerAngles.x, Is.EqualTo(48).Within(0.01f));
        }
        private static Vector3 PrefabSize(GameObject prefab)
        {
            var copy = Object.Instantiate(prefab);
            try { return SiteModelBuilder.Bounds(copy).size; }
            finally { Object.DestroyImmediate(copy); }
        }
        [Test]
        public void GeneratedMapsAreTileableAndImportedAsLinearDataExceptAlbedo()
        {
            // Exercise the fallback even on Bryan's machine where authored TerrainLayers exist.
            foreach (var kind in new[] { "Dirt", "Gravel", "Grass" }) SiteTextureBuilder.Generate(kind);
            SiteTextureBuilder.Dust();
            foreach (var path in Directory.GetFiles(SiteTextureBuilder.Folder, "*.png"))
            {
                var image = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
                try
                {
                    Assert.That(image.LoadImage(File.ReadAllBytes(path)), Is.True);
                    var pixels = image.GetPixels(); var size = image.width;
                    for (var i = 0; i < size; i++)
                    {
                        AssertChannels(pixels[i * size], pixels[i * size + size - 1], path);
                        AssertChannels(pixels[i], pixels[(size - 1) * size + i], path);
                    }
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path.Replace('\\', '/'));
                    if (path.Contains("_Normal") || path.Contains("_Mask")) Assert.That(importer.sRGBTexture, Is.False, path);
                    var android = importer.GetPlatformTextureSettings("Android");
                    Assert.That(android.overridden, Is.True); Assert.That(android.format, Is.EqualTo(TextureImporterFormat.ASTC_6x6));
                }
                finally { Object.DestroyImmediate(image); }
            }
        }
        private static void AssertChannels(Color a, Color b, string context)
        { for (var channel = 0; channel < 4; channel++) Assert.That(Mathf.Abs(a[channel] - b[channel]), Is.LessThanOrEqualTo(0.02f), context); }

        [UnityTest]
        public IEnumerator PipeStationPreviewStaysInsideQuestBudget()
        {
            var scene = PlaySceneBuilder.Build(false); var site = scene.GetRootGameObjects().Single(r => r.name == "[LOBOver] Site");
            var world = site.GetComponentInChildren<SiteWorld>(true); Populate(world, 8);
            foreach (var name in new[] { "Ground", "Horizon" })
            {
                var surface = site.transform.Find(name).GetComponent<Renderer>();
                Assert.That(surface.shadowCastingMode, Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off));
                Assert.That(surface.receiveShadows, Is.True);
            }
            // WO-L7: only the explicitly named survey view may move freely for WO-L4 statistics.
            var camera = scene.GetRootGameObjects().Single(r => r.name == "[LOBOver] Preview").GetComponent<LOBOver.Play.PlayPreview>().survey;
            var oldTarget = camera.targetTexture; var oldPosition = camera.transform.position; var oldRotation = camera.transform.rotation;
            // Scene view and the rig's centre eye are outside the owned roots. Include
            // hidden Editor cameras too; Scene view may explicitly render even disabled.
            var otherCameras = Resources.FindObjectsOfTypeAll<Camera>().Where(c => c != null && c != camera && !EditorUtility.IsPersistent(c))
                .Select(c => (camera: c, enabled: c.enabled, mask: c.cullingMask, clear: c.clearFlags)).ToArray();
            // OVR hides these at runtime; EditMode must exclude its controller/hand
            // variants from site statistics, restoring every enabled state afterwards.
            var rigRenderers = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .Where(t => t.name == "[BuildingBlock] Camera Rig")
                .SelectMany(r => r.GetComponentsInChildren<Renderer>(true)).Distinct().Where(r => r != null)
                .Select(r => (renderer: r, enabled: r.enabled)).ToArray();
            var crewShadows = world.GetComponentsInChildren<CrewRig>(true).SelectMany(c => c.GetComponentsInChildren<Renderer>(true))
                .Select(r => (renderer: r, mode: r.shadowCastingMode)).ToArray();
            var staticGeometry = site.GetComponentsInChildren<MeshFilter>(true)
                .Where(f => GameObjectUtility.AreStaticEditorFlagsSet(f.gameObject, StaticEditorFlags.BatchingStatic)
                    && f.sharedMesh != null && f.GetComponent<MeshRenderer>() != null)
                .Select(f => (filter: f, mesh: f.sharedMesh, renderer: f.GetComponent<MeshRenderer>(),
                    rendererState: EditorJsonUtility.ToJson(f.GetComponent<MeshRenderer>()))).ToArray();
            var enabled = camera.enabled; var active = site.activeSelf; var observer = world.sky.observer;
            var gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView", true);
            var gameView = Resources.FindObjectsOfTypeAll(gameViewType).Cast<EditorWindow>().FirstOrDefault();
            var createdGameView = gameView == null; var wasMaximized = gameView != null && gameView.maximized;
            var focusedWindow = EditorWindow.focusedWindow;
            var maximizedWindows = Resources.FindObjectsOfTypeAll<EditorWindow>().Where(w => w != gameView && w.maximized).ToArray();
            var cameraRenderCount = 0;
            void CountCameraRender(UnityEngine.Rendering.ScriptableRenderContext context, Camera renderedCamera)
            { if (renderedCamera == camera) cameraRenderCount++; }
            try
            {
                // SceneView can render despite a disabled camera. Hide it for the entire
                // measured frame by maximizing Game, then restore the Editor layout below.
                foreach (var window in maximizedWindows) window.maximized = false;
                if (gameView == null) gameView = EditorWindow.GetWindow(gameViewType);
                gameView.Focus(); gameView.maximized = true; gameView.Repaint();
                site.SetActive(true);
                foreach (var entry in otherCameras) if (entry.camera != null)
                { entry.camera.enabled = false; entry.camera.cullingMask = 0; entry.camera.clearFlags = CameraClearFlags.Nothing; }
                foreach (var entry in rigRenderers) if (entry.renderer != null) entry.renderer.enabled = false;
                Debug.Log($"WO-L4 excluded Camera Rig renderers: {rigRenderers.Length} total, {rigRenderers.Count(r => r.enabled)} enabled.");
                // Amendment 3: UnityStats counts Game view frames, not Camera.Render into a texture.
                UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += CountCameraRender;
                camera.enabled = true;
                camera.targetTexture = null;
                var x = (float)world.Crew("pipe").StationFt * WorldUnits.MetresPerFoot;
                camera.transform.position = new Vector3(x, 1.65f, WorldUnits.RoadZ);
                camera.transform.rotation = Quaternion.LookRotation(new Vector3(0, -0.12f, -1));
                world.sky.observer = camera.transform;
                foreach (var crew in world.GetComponentsInChildren<CrewRig>(true)) crew.RefreshDistancePresentation(camera.transform);
                // Match the player build's batching of only the builder's static-flagged geometry.
                StaticBatchingUtility.Combine(staticGeometry.Select(g => g.filter.gameObject).ToArray(), site);
                yield return null;
                gameView.Repaint();
                yield return null;
                // Amendment 4: the test runner may repaint this camera more than once per statistics window.
                var renderCount = Math.Max(1, cameraRenderCount);
                var triangles = UnityStats.triangles / renderCount; var drawCalls = UnityStats.drawCalls / renderCount;
                Debug.Log($"WO-L4 across trench Game view: {triangles} triangles, {drawCalls} draw calls.");
                Assert.That(gameView.maximized, Is.True, "Game view must hide SceneView for the measurement");
                Assert.That(triangles, Is.GreaterThan(0), "No Editor render statistics; run with the Game view visible.");
                Assert.That(drawCalls, Is.GreaterThan(0));
                Assert.That(triangles, Is.LessThan(300000)); Assert.That(drawCalls, Is.LessThan(150));
                foreach (var view in new[] { (crew: "exc", label: "excavator"), (crew: "back", label: "backfill crew") })
                {
                    cameraRenderCount = 0;
                    camera.transform.LookAt(world.Crew(view.crew).transform.position + Vector3.up);
                    gameView.Repaint();
                    yield return null;
                    renderCount = Math.Max(1, cameraRenderCount);
                    Debug.Log($"WO-L4 toward {view.label} Game view: {UnityStats.triangles / renderCount} triangles, {UnityStats.drawCalls / renderCount} draw calls.");
                }
            }
            finally
            {
                UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= CountCameraRender;
                if (camera != null)
                { camera.targetTexture = oldTarget; camera.transform.SetPositionAndRotation(oldPosition, oldRotation); camera.enabled = enabled; }
                if (world != null && world.sky != null) world.sky.observer = observer;
                foreach (var entry in otherCameras) if (entry.camera != null)
                { entry.camera.enabled = entry.enabled; entry.camera.cullingMask = entry.mask; entry.camera.clearFlags = entry.clear; }
                foreach (var entry in rigRenderers) if (entry.renderer != null) entry.renderer.enabled = entry.enabled;
                foreach (var entry in crewShadows) if (entry.renderer != null) entry.renderer.shadowCastingMode = entry.mode;
                if (world != null) foreach (var lod in world.GetComponentsInChildren<LODGroup>(true)) lod.ForceLOD(-1);
                // Combining replaces meshes and renderer batch metadata; leave neither behind in EditMode.
                var combinedMeshes = staticGeometry.Where(g => g.filter != null).Select(g => g.filter.sharedMesh)
                    .Where(m => m != null && !EditorUtility.IsPersistent(m) && !staticGeometry.Any(g => g.mesh == m)).Distinct().ToArray();
                foreach (var entry in staticGeometry)
                {
                    if (entry.filter != null) entry.filter.sharedMesh = entry.mesh;
                    if (entry.renderer != null) EditorJsonUtility.FromJsonOverwrite(entry.rendererState, entry.renderer);
                }
                foreach (var mesh in combinedMeshes) Object.DestroyImmediate(mesh);
                if (site != null) site.SetActive(active);
                if (gameView != null)
                {
                    gameView.maximized = wasMaximized;
                    if (createdGameView) gameView.Close();
                }
                foreach (var window in maximizedWindows) if (window != null) window.maximized = true;
                if (focusedWindow != null) focusedWindow.Focus();
            }
        }
        private static void Populate(SiteWorld world, int? displayDay = null)
        {
            var scenario = JObject.Parse(File.ReadAllText("Data/Scenarios/slice_texas.json"));
            var transcript = JObject.Parse(File.ReadAllText("Data/transcripts/slice_play.json"));
            var snapshots = (JArray)transcript["snapshots"];
            var snapshot = displayDay.HasValue ? snapshots.OfType<JObject>().Single(s => (int)Math.Floor((double)s["t"]) + 1 == displayDay.Value)
                : snapshots.OfType<JObject>().FirstOrDefault(s => ((JArray)s["site"]["trench"]).OfType<JObject>().Any(t => (string)t["state"] == "piped")) ?? (JObject)snapshots[1];
            world.Configure((JObject)scenario["job"], 0); world.Apply(SiteView.From((JObject)snapshot["site"]), displayDay ?? 4);
            foreach (var crew in WorldUnits.CrewIds) world.Crew(crew).SnapPose();
        }
    }
}
