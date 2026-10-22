using System;
using System.IO;
using System.Linq;
using LOBOver.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace LOBOver.Editor
{
    public static class SitePresentationBuilder
    {
        // WO-L4 item 7 / XR_RENDERING.md. Initial corridor length is LEVEL1.md's 3,000 ft.
        private const float SunElevation = 48, FogNear = 300, FogFar = 2500, InitialLength = 3000 * WorldUnits.MetresPerFoot;
        private const float MinimumCubemapSunElevation = 30; // WO-L4 Amendment 1: strictly above this elevation.
        private const int ShadowMapSize = 2048;
        public static readonly Color HorizonColour = new Color(0.68f, 0.73f, 0.75f);
        public static bool DaylightSkyName(string name) => !new[] { "Overcast", "Night", "Dusk", "Sunset", "Space" }
            .Any(word => name.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0);
        public static bool ElevatedCubemapSun(Vector3 direction) => direction.normalized.y > Mathf.Sin(MinimumCubemapSunElevation * Mathf.Deg2Rad);
        public static Material SkyMaterial()
        {
            var sky = SiteMaterialBuilder.Get(SiteMaterialBuilder.Folder + "SiteSky.mat", "LOBOver/SiteSky");
            Cubemap cube = null; Material source = null; var direction = Vector3.zero;
            var paths = AssetDatabase.FindAssets("t:Material", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !p.StartsWith(SiteAssetBuilder.Art, StringComparison.Ordinal) && DaylightSkyName(p)).OrderBy(p => p, StringComparer.Ordinal);
            foreach (var path in paths)
            {
                var candidate = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (candidate == null || candidate.shader == null || !DaylightSkyName(candidate.name)
                    || candidate.shader.name.IndexOf("Skybox", StringComparison.OrdinalIgnoreCase) < 0) continue;
                foreach (var property in candidate.GetTexturePropertyNames())
                    if (candidate.GetTexture(property) is Cubemap found && DaylightSkyName(found.name))
                    {
                        var probedSun = CubemapSun(found);
                        if (!ElevatedCubemapSun(probedSun)) continue;
                        cube = found; source = candidate; direction = probedSun; break;
                    }
                if (cube != null) break;
            }
            sky.SetTexture("_Cube", cube); sky.SetFloat("_UseCube", cube == null ? 0 : 1);
            sky.SetFloat("_Exposure", source != null && source.HasProperty("_Exposure") ? source.GetFloat("_Exposure") : 1);
            var rotation = source != null && source.HasProperty("_Rotation") ? source.GetFloat("_Rotation") : 0;
            sky.SetFloat("_Rotation", rotation);
            // SiteSky rotates the sampling direction by -rotation around Y; invert it for the light.
            sky.SetVector("_SunDirection", Quaternion.Euler(0, rotation, 0) * direction);
            return sky;
        }
        public static Vector3 CubemapSun(Cubemap cube)
        {
            // A tiny GPU readback also supports non-readable imports, without changing their importers.
            const int size = 64; // Editor-only sun search resolution, not a runtime rendering setting.
            var shader = Shader.Find("Hidden/LOBOver/CubemapSunProbe") ?? throw new InvalidOperationException("Missing cubemap sun probe shader");
            var probe = new Material(shader); probe.SetTexture("_Cube", cube);
            var target = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            var pixels = new Texture2D(size, size, TextureFormat.RGBAFloat, false, true);
            var previous = RenderTexture.active; var brightest = float.NegativeInfinity; var direction = Vector3.zero;
            try
            {
                foreach (var forward in new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back })
                {
                    var up = Mathf.Abs(forward.y) > 0.5f ? Vector3.forward : Vector3.up;
                    var right = Vector3.Cross(up, forward);
                    probe.SetVector("_FaceForward", forward); probe.SetVector("_FaceRight", right); probe.SetVector("_FaceUp", up);
                    Graphics.Blit(null, target, probe); RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, size, size), 0, 0); pixels.Apply();
                    var colors = pixels.GetPixels();
                    for (var y = 0; y < size; y++) for (var x = 0; x < size; x++)
                    {
                        var ray = (forward + right * (2 * (x + 0.5f) / size - 1) + up * (2 * (y + 0.5f) / size - 1)).normalized;
                        var color = colors[y * size + x]; var luminance = color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f;
                        if (luminance > brightest) { brightest = luminance; direction = ray; }
                    }
                }
                return direction;
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); Object.DestroyImmediate(pixels); Object.DestroyImmediate(probe); }
        }
        public static WorldAssets BuildAssets()
        {
            var legacy = SiteAssetBuilder.Required<WorldAssets>(SiteAssetBuilder.Art + "WorldAssets.asset");
            var path = SiteAssetBuilder.Art + "PlayWorldAssets.asset";
            var assets = AssetDatabase.LoadAssetAtPath<WorldAssets>(path);
            if (assets == null) { assets = ScriptableObject.CreateInstance<WorldAssets>(); AssetDatabase.CreateAsset(assets, path); }
            EditorUtility.CopySerialized(legacy, assets); assets.name = "PlayWorldAssets"; assets.detailedSite = true;
            Directory.CreateDirectory(SiteAssetBuilder.Converted); AssetDatabase.Refresh();
            SiteMaterialBuilder.Prepare(assets); SiteModelBuilder.Populate(assets, legacy);
            assets.groundMesh = SiteGeometryBuilder.Ground(); assets.horizonMesh = SiteGeometryBuilder.HorizonMesh();
            assets.stripMesh = SiteGeometryBuilder.Strip(); assets.wallNearMesh = SiteGeometryBuilder.Wall(1); assets.wallFarMesh = SiteGeometryBuilder.Wall(-1);
            assets.slumpMesh = SiteGeometryBuilder.Collapse(); assets.windrowMesh = SiteGeometryBuilder.Windrow();
            assets.cellPrefab = Cell(assets); assets.crewPrefab = Crew(); assets.particles = Dust(assets);
            EditorUtility.SetDirty(assets); AssetDatabase.SaveAssets(); return assets;
        }
        private static StationCell Cell(WorldAssets assets)
        {
            var root = new GameObject("DetailedStationCell");
            try
            {
                var cell = root.AddComponent<StationCell>(); SiteDetailDrawing.Cell(cell, assets);
                return SiteModelBuilder.SaveIfChanged(root, SiteAssetBuilder.Converted + "Play_StationCell.prefab").GetComponent<StationCell>();
            }
            finally { Object.DestroyImmediate(root); }
        }
        private static CrewRig Crew()
        {
            var root = new GameObject("DetailedCrewRig"); root.AddComponent<CrewRig>();
            try { return SiteModelBuilder.SaveIfChanged(root, SiteAssetBuilder.Converted + "Play_Crew.prefab").GetComponent<CrewRig>(); }
            finally { Object.DestroyImmediate(root); }
        }
        private static GameObject Dust(WorldAssets assets)
        {
            var root = new GameObject("WorkParticles");
            try
            {
                var ps = root.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main; main.loop = true; main.playOnAwake = false; main.startLifetime = 1.8f;
                main.startSpeed = 0.35f; main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
                main.startColor = new Color(0.65f, 0.52f, 0.36f, 0.35f); main.maxParticles = 60;
                var emission = ps.emission; emission.rateOverTime = 18;
                var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(2, 0.15f, 1);
                var alpha = ps.colorOverLifetime; alpha.enabled = true;
                var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                    new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(0.6f, 0.15f), new GradientAlphaKey(0, 1) }); alpha.color = gradient;
                root.transform.localPosition = new Vector3(-1, 0.2f, 0);
                var renderer = ps.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = assets.ink;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                return SiteModelBuilder.SaveIfChanged(root, SiteAssetBuilder.Converted + "Play_WorkParticles.prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }
        public static SiteSky Build(Transform site, WorldAssets assets)
        {
            var ground = SiteDetailDrawing.Mesh("Ground", site, assets.groundMesh, assets.ground);
            var horizon = SiteDetailDrawing.Mesh("Horizon", site, assets.horizonMesh, assets.ground);
            // WO-L4 rejection 3: these continuous surfaces receive, but need not cast.
            foreach (var surface in new[] { ground, horizon })
            { var renderer = surface.GetComponent<Renderer>(); renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = true; }
            // Seal the corridor beyond the longest authored site without covering host-driven cells.
            var after = SiteDetailDrawing.Mesh("BeyondTrench", site, assets.stripMesh, assets.soil, new Vector3(InitialLength, 0, 0));
            after.transform.localScale = new Vector3((SiteGeometryBuilder.MaxX - InitialLength) / WorldUnits.CellLength, 1, 1);
            var sky = Sky(site, assets);
            var trailer = WorldDrawing.Group("Trailer", site).transform; trailer.localPosition = new Vector3(-16, 0, 12);
            SiteDetailDrawing.Block("Cabin", trailer, new Vector3(0, 1.5f, 0), new Vector3(7, 3, 2.8f), assets.cabin);
            SiteDetailDrawing.Block("Roof", trailer, new Vector3(0, 3.08f, 0), new Vector3(7.25f, 0.16f, 3), assets.steel);
            SiteDetailDrawing.Block("DoorFrame", trailer, new Vector3(-1.7f, 1.15f, -1.43f), new Vector3(1.05f, 2.3f, 0.06f), assets.steel);
            SiteDetailDrawing.Block("Door", trailer, new Vector3(-1.7f, 1.15f, -1.47f), new Vector3(0.9f, 2.18f, 0.025f), assets.cabin);
            SiteDetailDrawing.Block("DoorHandle", trailer, new Vector3(-1.36f, 1.1f, -1.5f), new Vector3(0.04f, 0.18f, 0.04f), assets.steel);
            SiteDetailDrawing.Block("WindowFrame", trailer, new Vector3(1.35f, 1.75f, -1.44f), new Vector3(2.1f, 1.25f, 0.08f), assets.steel);
            SiteDetailDrawing.Block("Window", trailer, new Vector3(1.35f, 1.75f, -1.49f), new Vector3(1.94f, 1.09f, 0.02f), assets.glass);
            foreach (var x in new[] { -2.5f, 2.5f }) SiteDetailDrawing.Block("Support", trailer, new Vector3(x, 0.12f, 0), new Vector3(0.4f, 0.24f, 2.4f), assets.steel);
            foreach (var z in new[] { WorldUnits.RoadZ - 3, WorldUnits.RoadZ + 3 })
                SiteDetailDrawing.Prop(assets.jersey, site, new Vector3(-8, 0, z));
            if (assets.tree != null)
            {
                // WO-L4: fewer than thirty instances, 80..120 m on the far side, with gaps.
                for (var i = 0; i < 24; i++)
                {
                    if (i % 7 == 3) continue;
                    var tree = SiteDetailDrawing.Prop(assets.tree, site, new Vector3(-70 + i * 49, 0, -80 - i * 17 % 40));
                    tree.transform.localPosition += Vector3.up * SiteGeometryBuilder.Height(tree.transform.localPosition.x, tree.transform.localPosition.z);
                    tree.transform.localRotation = Quaternion.Euler(0, i * 137, 0);
                }
            }
            foreach (var renderer in site.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.transform.IsChildOf(sky.transform)) continue;
                // Trees remain instanced. The ground and cabin are builder-owned static geometry.
                if (renderer.GetComponentInParent<LODGroup>() != null || renderer.transform.root != site.root) continue;
                if (!renderer.transform.IsChildOf(trailer) && renderer.gameObject != ground && renderer.name != "Horizon" && renderer.name != "BeyondTrench") continue;
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, StaticEditorFlags.BatchingStatic);
            }
            return sky;
        }
        private static SiteSky Sky(Transform parent, WorldAssets assets)
        {
            var root = WorldDrawing.Group("SiteSky", parent); var sky = root.AddComponent<SiteSky>();
            var dome = GameObject.CreatePrimitive(PrimitiveType.Sphere); dome.name = "SiteBackdrop"; dome.transform.SetParent(root.transform, false);
            dome.transform.localPosition = new Vector3(500, 0, 0); dome.transform.localScale = Vector3.one * 10000;
            Object.DestroyImmediate(dome.GetComponent<Collider>());
            var renderer = dome.GetComponent<Renderer>(); renderer.sharedMaterial = assets.skyMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            sky.dome = dome.transform;
            sky.cloudBank = WorldDrawing.Group("CloudBank", root.transform).transform;
            var rain = WorldDrawing.Group("Rain", root.transform).AddComponent<ParticleSystem>();
            rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = rain.main; main.loop = true; main.playOnAwake = false; main.startLifetime = 1.2f; main.startSpeed = 24;
            main.startSize = 0.025f; main.maxParticles = 1200; main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = rain.emission; emission.rateOverTime = 800;
            var shape = rain.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(24, 24, 1);
            rain.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var particles = rain.GetComponent<ParticleSystemRenderer>(); particles.sharedMaterial = assets.rain;
            particles.renderMode = ParticleSystemRenderMode.Stretch; particles.lengthScale = 5; particles.velocityScale = 0.02f;
            particles.shadowCastingMode = ShadowCastingMode.Off; sky.rain = rain; rain.gameObject.SetActive(false); return sky;
        }
        public static void Lighting(Light sun)
        {
            sun.transform.rotation = Quaternion.Euler(SunElevation, -35, 0); sun.color = new Color(1, 0.93f, 0.82f);
            var sky = AssetDatabase.LoadAssetAtPath<Material>(SiteMaterialBuilder.Folder + "SiteSky.mat");
            if (sky != null && sky.GetFloat("_UseCube") > 0.5f)
            {
                Vector3 direction = sky.GetVector("_SunDirection");
                if (sky.GetTexture("_Cube") != null && ElevatedCubemapSun(direction))
                    sun.transform.rotation = Quaternion.LookRotation(-direction, Mathf.Abs(direction.y) > 0.99f ? Vector3.forward : Vector3.up);
                else
                {
                    // Old generated materials must not bring back the horizon sun on a rebuild.
                    sky.SetFloat("_UseCube", 0); sky.SetTexture("_Cube", null); EditorUtility.SetDirty(sky);
                }
            }
            sun.intensity = 1.2f; sun.shadows = LightShadows.Soft; sun.shadowCustomResolution = ShadowMapSize;
            RenderSettings.sun = sun; RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.49f, 0.59f, 0.70f);
            RenderSettings.ambientEquatorColor = HorizonColour * 0.65f; RenderSettings.ambientGroundColor = new Color(0.24f, 0.20f, 0.16f);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogColor = HorizonColour;
            RenderSettings.fogStartDistance = FogNear; RenderSettings.fogEndDistance = FogFar;
        }
    }
}
