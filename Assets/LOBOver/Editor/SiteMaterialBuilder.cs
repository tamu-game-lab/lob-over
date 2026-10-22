using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LOBOver.World;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LOBOver.Editor
{
    public static class SiteMaterialBuilder
    {
        public const string Folder = SiteAssetBuilder.Art + "Materials/";
        private static readonly Dictionary<Material, Material> Copies = new Dictionary<Material, Material>();
        private static SiteTextureBuilder.Layer dirt, gravel, grass;
        private static WorldAssets assets;
        public static void Prepare(WorldAssets target)
        {
            assets = target; Copies.Clear(); Directory.CreateDirectory(Folder); Directory.CreateDirectory(SiteTextureBuilder.Folder);
            AssetDatabase.Refresh(); dirt = SiteTextureBuilder.FindLayer("Dirt"); gravel = SiteTextureBuilder.FindLayer("Gravel"); grass = SiteTextureBuilder.FindLayer("Grass");
            assets.soil = Surface("Soil", Color.white, dirt, true);
            assets.road = Surface("AccessRoad", Color.white, gravel, true);
            assets.ground = Surface("Ground", Color.white, dirt, true); assets.ground.EnableKeyword("_Splat"); assets.ground.SetFloat("_Splat", 1);
            assets.ground.SetTexture("_GravelMap", gravel.albedo); assets.ground.SetTexture("_GravelNormal", gravel.normal); assets.ground.SetTexture("_GravelMask", gravel.mask);
            assets.ground.SetTexture("_GrassMap", grass.albedo); assets.ground.SetTexture("_GrassNormal", grass.normal); assets.ground.SetTexture("_GrassMask", grass.mask);
            assets.timber = Surface("Timber", new Color(0.60f, 0.43f, 0.24f), dirt);
            assets.timber.SetTextureScale("_BaseMap", new Vector2(0.25f, 4));
            assets.steel = Surface("StorageSteel", new Color(0.39f, 0.43f, 0.45f), gravel); assets.steel.SetFloat("_Metallic", 0.7f); assets.steel.DisableKeyword("_METALLICSPECGLOSSMAP");
            assets.cabin = Surface("Repair_SiteCabin", new Color(0.74f, 0.72f, 0.64f), gravel);
            assets.cabin.SetTextureScale("_BaseMap", new Vector2(4, 0.25f));
            assets.glass = Surface("CabinWindow", new Color(0.09f, 0.15f, 0.18f), dirt); assets.glass.SetFloat("_Smoothness", 0.9f);
            assets.orange = Surface("SafetyOrange", new Color(0.95f, 0.36f, 0.045f), dirt);
            assets.flagging = Surface("PinkFlagging", new Color(1, 0.12f, 0.46f), dirt);
            // WO-L4 rejection 3: groundTrenchDig is a UV atlas, never a tiled surface.
            assets.trench = Surface("TrenchWall", new Color(0.80f, 0.75f, 0.70f), dirt, true);
            assets.bedding = Surface("Bedding", Color.white, gravel, true);
            assets.backfill = Surface("DisturbedBackfill", new Color(0.8f, 0.72f, 0.62f), dirt, true);
            assets.wetSoil = Surface("WetTrench", new Color(0.45f, 0.42f, 0.40f), dirt, true);
            const float trenchTileMetres = 1; // WO-L4 Amendment 2, all cut surfaces.
            foreach (var material in new[] { assets.trench, assets.bedding, assets.wetSoil }) material.SetFloat("_TileMetres", trenchTileMetres);
            assets.wetSoil.SetFloat("_Smoothness", 0.8f);
            assets.waterSurface = Surface("StandingWater", new Color(0.06f, 0.085f, 0.09f), dirt, true);
            assets.waterSurface.SetFloat("_Smoothness", 0.95f); assets.waterSurface.SetFloat("_BumpScale", 0.08f);
            assets.waterSurface.DisableKeyword("_METALLICSPECGLOSSMAP"); assets.waterSurface.SetFloat("_Metallic", 0.35f);
            // WO-L4 rejection 1: brown earth tint while retaining the sand normal and AO.
            assets.spoilSurface = Surface("Spoil", new Color(0.55f, 0.45f, 0.35f), dirt, true);
            var sand = SiteAssetBuilder.Imported + "3D_Models/a-pile-of-sand/";
            assets.spoilSurface.SetTexture("_BaseMap", Texture(sand, "albedo") ?? dirt.albedo);
            assets.spoilSurface.SetTexture("_BumpMap", SiteTextureBuilder.LinearCopy(Texture(sand, "NormalsMap"), true) ?? dirt.normal);
            assets.spoilSurface.SetTexture("_OcclusionMap", SiteTextureBuilder.LinearCopy(Texture(sand, "occlusion"), false) ?? dirt.mask); assets.spoilSurface.EnableKeyword("_OCCLUSIONMAP");
            assets.spoilSurface.SetFloat("_OcclusionFromMask", 0);
            PackOcclusion(assets.spoilSurface);
            assets.ink = Particle("Dust", new Color(0.65f, 0.54f, 0.40f, 0.4f));
            assets.rain = Particle("Rain", new Color(0.6f, 0.67f, 0.7f, 0.3f));
            assets.siteText = Get(Folder + "SiteLettering.mat", "LOBOver/SiteLit");
            var letteringShader = assets.siteText.shader;
            // WO-L5: the SDF branch preserves lettering and participates in the same global wave.
            EditorUtility.CopySerialized(assets.font.material, assets.siteText);
            assets.siteText.name = "SiteLettering"; assets.siteText.shader = letteringShader;
            assets.siteText.SetColor("_FaceColor", new Color(0.96f, 0.95f, 0.88f));
            assets.siteText.EnableKeyword("_SITE_TEXT"); assets.siteText.SetFloat("_SiteText", 1);
            assets.siteText.SetTexture("_BaseMap", assets.font.atlasTexture);
            assets.siteText.SetFloat("_Cull", 0);
            assets.siteText.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(assets.siteText);
            assets.skyMaterial = SitePresentationBuilder.SkyMaterial();
            // Keep the established converted material identities usable by old prefab references.
            foreach (var pair in new[] { ("Soil", assets.soil), ("AccessRoad", assets.road), ("Timber", assets.timber),
                ("StorageSteel", assets.steel), ("Repair_SiteCabin", assets.cabin) })
            {
                var old = AssetDatabase.LoadAssetAtPath<Material>(SiteAssetBuilder.Converted + pair.Item1 + ".mat");
                if (old == null) continue;
                EditorUtility.CopySerialized(pair.Item2, old); old.name = pair.Item1; EditorUtility.SetDirty(old);
            }
        }
        public static Material Surface(string name, Color color, SiteTextureBuilder.Layer layer = null, bool world = false)
        {
            layer ??= dirt;
            var material = Get(Folder + name + ".mat", "LOBOver/SiteLit");
            material.shaderKeywords = Array.Empty<string>(); material.SetColor("_BaseColor", color);
            material.SetTexture("_BaseMap", layer.albedo); material.SetTexture("_BumpMap", layer.normal); material.SetTexture("_MetallicGlossMap", layer.mask);
            material.SetTextureScale("_BaseMap", Vector2.one); material.SetTextureOffset("_BaseMap", Vector2.zero);
            material.SetFloat("_Metallic", 0); material.SetFloat("_Smoothness", 0.4f); material.SetFloat("_BumpScale", 1);
            material.SetFloat("_OcclusionFromMask", 1);
            material.SetFloat("_Splat", 0); material.SetFloat("_World", world ? 1 : 0); material.SetFloat("_TileMetres", Mathf.Min(layer.metres, 4));
            material.SetFloat("_AlphaClip", 0); material.SetFloat("_UseAlphaMap", 0); material.SetFloat("_Cull", 2);
            material.EnableKeyword("_METALLICSPECGLOSSMAP"); if (world) material.EnableKeyword("_WORLD");
            return material;
        }
        public static Material Get(string path, string shaderName)
        {
            var shader = Shader.Find(shaderName) ?? throw new InvalidOperationException("Missing shader " + shaderName);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.shader = shader; material.enableInstancing = true; EditorUtility.SetDirty(material); return material;
        }
        public static void PackOcclusion(Material material)
        {
            if (material.GetFloat("_OcclusionFromMask") > 0.5f) return;
            if (!(material.GetTexture("_OcclusionMap") is Texture2D occlusion)) return;
            material.SetTexture("_MetallicGlossMap", SiteTextureBuilder.PackOcclusion(material.GetTexture("_MetallicGlossMap") as Texture2D, occlusion));
            material.SetFloat("_OcclusionFromMask", 1);
            material.DisableKeyword("_OCCLUSIONMAP");
        }
        private static Material Particle(string name, Color color)
        {
            var m = Get(Folder + name + ".mat", "Universal Render Pipeline/Particles/Unlit");
            m.SetTexture("_BaseMap", SiteTextureBuilder.Dust()); m.SetColor("_BaseColor", color);
            m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0); m.SetFloat("_ZWrite", 0);
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.SetOverrideTag("RenderType", "Transparent"); m.renderQueue = (int)RenderQueue.Transparent;
            return m;
        }
        public static Texture2D Texture(string folder, string token)
        {
            if (!AssetDatabase.IsValidFolder(folder.TrimEnd('/'))) return null;
            var path = AssetDatabase.FindAssets("t:Texture2D", new[] { folder.TrimEnd('/') }).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => Path.GetFileName(p).IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0).OrderBy(p => p, StringComparer.Ordinal).FirstOrDefault();
            return path == null ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        public static void Convert(GameObject root)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is ParticleSystemRenderer particle)
                { particle.sharedMaterial = assets.ink; particle.trailMaterial = assets.ink; continue; }
                if (renderer.GetComponent<TMP_Text>() is TMP_Text text) { text.fontSharedMaterial = assets.siteText; continue; }
                renderer.sharedMaterials = renderer.sharedMaterials.Select(Copy).ToArray();
            }
        }
        public static Material Copy(Material source)
        {
            if (source == null) return assets.steel;
            if (source.shader.name == "LOBOver/SiteLit") return source;
            if (Copies.TryGetValue(source, out var previous)) return previous;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string guid, out long id);
            var name = string.Concat(source.name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            var copy = Get(SiteAssetBuilder.Converted + name + "-Site-" + guid + "-" + id + ".mat", "LOBOver/SiteLit");
            var baseProperty = source.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
            var colorProperty = source.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
            var folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(source)).Replace('\\', '/');
            Texture Map(string property) => source.HasProperty(property) ? source.GetTexture(property) : null;
            var albedo = Map(baseProperty) ?? Texture(folder, "BaseColor") ?? Texture(folder, "Albedo") ?? dirt.albedo;
            copy.shaderKeywords = Array.Empty<string>(); copy.SetTexture("_BaseMap", albedo);
            copy.SetColor("_BaseColor", source.HasProperty(colorProperty) ? source.GetColor(colorProperty) : Color.white);
            if (source.HasProperty(baseProperty)) { copy.SetTextureScale("_BaseMap", source.GetTextureScale(baseProperty)); copy.SetTextureOffset("_BaseMap", source.GetTextureOffset(baseProperty)); }
            copy.SetTexture("_BumpMap", SiteTextureBuilder.LinearCopy((Map("_BumpMap") ?? Texture(folder, "Normal")) as Texture2D, true) ?? dirt.normal);
            var metallic = Map("_MetallicGlossMap"); copy.SetTexture("_MetallicGlossMap", SiteTextureBuilder.LinearCopy(metallic as Texture2D, false) ?? dirt.mask);
            copy.SetFloat("_Metallic", source.HasProperty("_Metallic") ? source.GetFloat("_Metallic") : 0);
            copy.SetFloat("_Smoothness", source.HasProperty("_Smoothness") ? source.GetFloat("_Smoothness") : source.HasProperty("_Glossiness") ? source.GetFloat("_Glossiness") : 0.3f);
            if (metallic != null) { copy.EnableKeyword("_METALLICSPECGLOSSMAP"); if (source.HasProperty("_GlossMapScale")) copy.SetFloat("_Smoothness", source.GetFloat("_GlossMapScale")); }
            var occlusion = Map("_OcclusionMap") ?? Texture(folder, "Occlusion"); copy.SetTexture("_OcclusionMap", SiteTextureBuilder.LinearCopy(occlusion as Texture2D, false));
            if (occlusion != null) copy.EnableKeyword("_OCCLUSIONMAP");
            copy.SetFloat("_OcclusionFromMask", 0); PackOcclusion(copy);
            foreach (var property in new[] { "_BumpMap", "_MetallicGlossMap", "_OcclusionMap" })
                if (source.HasProperty(property)) { copy.SetTextureScale(property, source.GetTextureScale(property)); copy.SetTextureOffset(property, source.GetTextureOffset(property)); }
            copy.SetFloat("_World", 0); copy.SetFloat("_Splat", 0); copy.SetFloat("_UseAlphaMap", 0);
            if (source.IsKeywordEnabled("_ALPHATEST_ON") || source.GetTag("RenderType", false) == "TransparentCutout")
            { copy.EnableKeyword("_ALPHATEST_ON"); copy.SetFloat("_AlphaClip", 1); copy.SetFloat("_Cull", 0); }
            Copies.Add(source, copy); return copy;
        }
    }
}
