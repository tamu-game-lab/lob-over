using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LOBOver.Editor
{
    public static class SiteTextureBuilder
    {
        public const string Folder = SiteAssetBuilder.Art + "Generated/";
        public const int Size = 1024, DustSize = 256; // WO-L4 item 3.
        public sealed class Layer
        {
            public Texture2D albedo, normal, mask;
            public float metres = 2;
        }
        public static Layer FindLayer(string kind)
        {
            // Prefer the named terrain pack, then any authored TerrainLayer with all surface maps.
            var preferences = kind == "Dirt" ? new[] { "Muddy", "Soil_Rocks", "dirt", "soil", "mud" }
                : kind == "Gravel" ? new[] { "Pebbles_A", "Pebbles_B", "Pebbles_C", "gravel", "pebbles" }
                : new[] { "Grass_Dry", "dry_grass", "grass" };
            var layers = AssetDatabase.FindAssets("t:TerrainLayer", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => p, StringComparer.Ordinal).Select(AssetDatabase.LoadAssetAtPath<TerrainLayer>).ToArray();
            foreach (var word in preferences)
            {
                var layer = layers.FirstOrDefault(l => l != null && l.diffuseTexture != null && l.normalMapTexture != null
                    && l.name.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0);
                if (layer != null)
                    return new Layer { albedo = layer.diffuseTexture, normal = layer.normalMapTexture,
                        mask = layer.maskMapTexture ?? Solid("DryMask", new Color(0, 1, 0, 0.3f), false), metres = Mathf.Max(1, layer.tileSize.x) };
            }
            var textures = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !p.StartsWith(Folder, StringComparison.Ordinal)).OrderBy(p => p, StringComparer.Ordinal).ToArray();
            foreach (var word in preferences)
            {
                var albedo = textures.FirstOrDefault(p => Path.GetFileName(p).IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0
                    && (p.IndexOf("basecolor", StringComparison.OrdinalIgnoreCase) >= 0 || p.IndexOf("albedo", StringComparison.OrdinalIgnoreCase) >= 0));
                if (albedo == null) continue;
                var normal = textures.FirstOrDefault(p => Path.GetDirectoryName(p) == Path.GetDirectoryName(albedo)
                    && Path.GetFileName(p).IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0 && p.IndexOf("normal", StringComparison.OrdinalIgnoreCase) >= 0);
                if (normal != null) return new Layer { albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedo),
                    normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normal), mask = Solid("DryMask", new Color(0, 1, 0, 0.3f), false) };
            }
            return Generate(kind);
        }
        public static Texture2D Solid(string name, Color colour, bool colourData = true)
        {
            var path = Folder + name + ".png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path); if (existing != null) return existing;
            return Write(path, Enumerable.Repeat(colour, 16).ToArray(), 4, colourData, false);
        }
        public static Texture2D Dust()
        {
            var path = Folder + "Dust.png"; var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;
            var pixels = new Color[DustSize * DustSize];
            for (var y = 0; y < DustSize; y++) for (var x = 0; x < DustSize; x++)
            {
                var u = 2f * x / (DustSize - 1) - 1; var v = 2f * y / (DustSize - 1) - 1;
                var alpha = Mathf.Pow(Mathf.Clamp01(1 - u * u - v * v), 3);
                pixels[y * DustSize + x] = new Color(0.47f, 0.35f, 0.22f, alpha);
            }
            return Write(path, pixels, DustSize, true, false);
        }
        public static Texture2D LinearCopy(Texture2D source, bool normal)
        {
            if (source == null) return null;
            var sourcePath = AssetDatabase.GetAssetPath(source);
            // Terrain and sky packs are always referenced in place. Only LOBGame conversions
            // may copy maps, and their source import settings are never changed.
            if (!sourcePath.StartsWith(SiteAssetBuilder.Imported, StringComparison.Ordinal)) return source;
            var path = SiteAssetBuilder.Converted + AssetDatabase.AssetPathToGUID(sourcePath) + (normal ? "_Normal" : "_Linear") + Path.GetExtension(sourcePath);
            if (!File.Exists(path)) { File.Copy(sourcePath, path); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport); }
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            var android = importer.GetPlatformTextureSettings("Android");
            if (importer.textureType != type || importer.sRGBTexture || !android.overridden || android.format != TextureImporterFormat.ASTC_6x6)
            {
                importer.textureType = type; importer.sRGBTexture = false;
                importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = "Android", overridden = true,
                    maxTextureSize = Size, format = TextureImporterFormat.ASTC_6x6 }); importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        public static Texture2D MetallicRoughness(Texture2D metallic, Texture2D roughness)
        {
            var path = SiteAssetBuilder.Converted + "Pipe_MetallicSmoothness.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path); if (existing != null) return existing;
            var m = ReadLinear(metallic, Size); var r = ReadLinear(roughness, Size);
            try
            {
                var metal = m.GetPixels(); var rough = r.GetPixels();
                for (var i = 0; i < metal.Length; i++) metal[i] = new Color(metal[i].r, 1, 0, 1 - rough[i].r);
                return Write(path, metal, Size, false, false);
            }
            finally { UnityEngine.Object.DestroyImmediate(m); UnityEngine.Object.DestroyImmediate(r); }
        }
        public static Texture2D PackOcclusion(Texture2D mask, Texture2D occlusion)
        {
            var path = SiteAssetBuilder.Converted + "Mask_" + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(mask))
                + "_" + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(occlusion)) + ".png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path); if (existing != null) return existing;
            var m = ReadLinear(mask, Size); var o = ReadLinear(occlusion, Size);
            try
            {
                var values = m.GetPixels(); var occ = o.GetPixels();
                for (var i = 0; i < values.Length; i++) values[i].g = occ[i].g;
                return Write(path, values, Size, false, false);
            }
            finally { UnityEngine.Object.DestroyImmediate(m); UnityEngine.Object.DestroyImmediate(o); }
        }
        public static Texture2D PackOpacity(Texture2D albedo, Texture2D opacity)
        {
            var path = SiteAssetBuilder.Converted + "Foliage_" + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(albedo))
                + "_" + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(opacity)) + ".png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path); if (existing != null) return existing;
            var a = ReadLinear(albedo, Size); var o = ReadLinear(opacity, Size);
            try
            {
                var values = a.GetPixels(); var alpha = o.GetPixels();
                for (var i = 0; i < values.Length; i++)
                {
                    if (QualitySettings.activeColorSpace == ColorSpace.Linear) values[i] = values[i].gamma;
                    values[i].a = alpha[i].r;
                }
                return Write(path, values, Size, true, false);
            }
            finally { UnityEngine.Object.DestroyImmediate(a); UnityEngine.Object.DestroyImmediate(o); }
        }
        private static Texture2D ReadLinear(Texture2D texture, int size)
        {
            var rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var previous = RenderTexture.active;
            try
            {
                Graphics.Blit(texture, rt); RenderTexture.active = rt;
                var result = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
                result.ReadPixels(new Rect(0, 0, size, size), 0, 0); result.Apply(); return result;
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt); }
        }
        public static Layer Generate(string kind)
        {
            var prefix = Folder + kind;
            if (File.Exists(prefix + "_Albedo.png") && File.Exists(prefix + "_Normal.png") && File.Exists(prefix + "_Mask.png"))
                return new Layer { albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(prefix + "_Albedo.png"),
                    normal = AssetDatabase.LoadAssetAtPath<Texture2D>(prefix + "_Normal.png"), mask = AssetDatabase.LoadAssetAtPath<Texture2D>(prefix + "_Mask.png") };
            var heights = new float[Size * Size]; var colors = new Color[heights.Length]; var masks = new Color[heights.Length];
            var low = kind == "Grass" ? new Color(0.25f, 0.27f, 0.13f) : kind == "Gravel" ? new Color(0.25f, 0.24f, 0.21f) : new Color(0.25f, 0.17f, 0.10f);
            var high = kind == "Grass" ? new Color(0.60f, 0.53f, 0.30f) : kind == "Gravel" ? new Color(0.65f, 0.61f, 0.53f) : new Color(0.57f, 0.43f, 0.27f);
            for (var y = 0; y < Size; y++) for (var x = 0; x < Size; x++)
            {
                var u = x / (float)(Size - 1); var v = y / (float)(Size - 1);
                var height = Fbm(u, v, kind == "Grass" ? 96 : 12, kind == "Grass" ? 6 : 12);
                if (kind == "Gravel") height = 0.65f * Worley(u, v, 48) + 0.35f * height;
                var grain = Periodic(u, v, 256, 256);
                heights[y * Size + x] = height;
                colors[y * Size + x] = Color.Lerp(low, high, Mathf.Clamp01(height * 0.85f + grain * 0.15f));
                masks[y * Size + x] = new Color(0, Mathf.Lerp(0.7f, 1, height), 0, Mathf.Lerp(0.12f, 0.38f, grain));
            }
            var normals = new Color[heights.Length];
            for (var y = 0; y < Size; y++) for (var x = 0; x < Size; x++)
            {
                // Wrap the unique period (Size-1); duplicated last row/column is exactly seamless.
                var left = (x + Size - 2) % (Size - 1); var right = (x + 1) % (Size - 1);
                var down = (y + Size - 2) % (Size - 1); var up = (y + 1) % (Size - 1);
                var n = new Vector3((heights[y * Size + left] - heights[y * Size + right]) * 5,
                    (heights[down * Size + x] - heights[up * Size + x]) * 5, 1).normalized;
                normals[y * Size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1);
            }
            return new Layer { albedo = Write(prefix + "_Albedo.png", colors, Size, true, false),
                normal = Write(prefix + "_Normal.png", normals, Size, false, true), mask = Write(prefix + "_Mask.png", masks, Size, false, false) };
        }
        private static float Hash(int x, int y)
        {
            unchecked { uint n = (uint)(x * 374761393 + y * 668265263); n = (n ^ n >> 13) * 1274126177; return (n ^ n >> 16) / (float)uint.MaxValue; }
        }
        private static int Wrap(int x, int period) => (x % period + period) % period;
        private static float Periodic(float u, float v, int nx, int ny)
        {
            var x = u * nx; var y = v * ny; var ix = Mathf.FloorToInt(x); var iy = Mathf.FloorToInt(y);
            var fx = Mathf.SmoothStep(0, 1, x - ix); var fy = Mathf.SmoothStep(0, 1, y - iy);
            return Mathf.Lerp(Mathf.Lerp(Hash(Wrap(ix, nx), Wrap(iy, ny)), Hash(Wrap(ix + 1, nx), Wrap(iy, ny)), fx),
                Mathf.Lerp(Hash(Wrap(ix, nx), Wrap(iy + 1, ny)), Hash(Wrap(ix + 1, nx), Wrap(iy + 1, ny)), fx), fy);
        }
        private static float Fbm(float u, float v, int nx, int ny) => Periodic(u, v, nx, ny) * 0.57f
            + Periodic(u, v, nx * 2, ny * 2) * 0.28f + Periodic(u, v, nx * 4, ny * 4) * 0.15f;
        private static float Worley(float u, float v, int count)
        {
            var x = u * count; var y = v * count; var ix = Mathf.FloorToInt(x); var iy = Mathf.FloorToInt(y); var distance = 2f;
            for (var j = -1; j <= 1; j++) for (var i = -1; i <= 1; i++)
            {
                var px = ix + i + Hash(Wrap(ix + i, count), Wrap(iy + j, count));
                var py = iy + j + Hash(Wrap(iy + j, count) + 17, Wrap(ix + i, count));
                distance = Mathf.Min(distance, Vector2.Distance(new Vector2(x, y), new Vector2(px, py)));
            }
            return Mathf.SmoothStep(0, 1, 1 - distance);
        }
        public static Texture2D Write(string path, Color[] pixels, int size, bool srgb, bool normal)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, !srgb);
            texture.SetPixels(pixels); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.sRGBTexture = srgb; importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.wrapMode = TextureWrapMode.Repeat; importer.mipmapEnabled = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = "Android", overridden = true,
                maxTextureSize = Size, format = TextureImporterFormat.ASTC_6x6 });
            importer.SaveAndReimport(); return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
