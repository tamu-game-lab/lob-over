using System;
using System.Collections.Generic;
using LOBOver.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace LOBOver.Editor
{
    public static class SiteTreeBuilder
    {
        private const int BakeSize = 512; // WO-L4 rejection 3: one albedo/alpha bake, crossed quads.
        private const float FramePadding = 1.05f; // Bake border prevents clipped leaves and mip wrap.
        public static GameObject Build(GameObject source)
        {
            // Imported prefab assets need an instance to evaluate renderer world bounds.
            var instance = Object.Instantiate(source);
            Bounds bounds;
            try { bounds = SiteModelBuilder.Bounds(instance); }
            finally { Object.DestroyImmediate(instance); }
            var span = Mathf.Max(bounds.size.x, bounds.size.y) * FramePadding;
            var path = SiteTextureBuilder.Folder + "TreeBillboard_Albedo.png";
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(path) ?? Bake(source, bounds, span, path);
            var layer = new SiteTextureBuilder.Layer { albedo = albedo,
                normal = SiteTextureBuilder.Solid("TreeBillboard_Normal", new Color(0.5f, 0.5f, 1, 1), false),
                mask = SiteTextureBuilder.Solid("TreeBillboard_Mask", new Color(0, 1, 0, 0), false) };
            var material = SiteMaterialBuilder.Surface("TreeBillboard", Color.white, layer);
            material.SetFloat("_BumpScale", 1); material.SetFloat("_AlphaClip", 1); // Orchestrator fix: a bump scale of 0 renders the card white in LOBOver/SiteLit (verified 2026-10-22); the flat normal map makes 1 equivalent. material.SetFloat("_Cull", 0);
            material.EnableKeyword("_ALPHATEST_ON"); material.enableInstancing = true;
            material.SetOverrideTag("RenderType", "TransparentCutout"); material.renderQueue = (int)RenderQueue.AlphaTest;
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var indices = new List<int>();
            foreach (var axis in new[] { Vector3.right, Vector3.forward })
            {
                var start = vertices.Count;
                vertices.Add(bounds.center - axis * span / 2 - Vector3.up * span / 2);
                vertices.Add(bounds.center + axis * span / 2 - Vector3.up * span / 2);
                vertices.Add(bounds.center + axis * span / 2 + Vector3.up * span / 2);
                vertices.Add(bounds.center - axis * span / 2 + Vector3.up * span / 2);
                uv.AddRange(new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up });
                indices.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
            }
            var mesh = new Mesh(); mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            var root = new GameObject("Tree");
            try
            {
                root.AddComponent<MeshFilter>().sharedMesh = SiteGeometryBuilder.Save("TreeCrossedQuads", mesh);
                var renderer = root.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                return SiteModelBuilder.SaveIfChanged(root, SiteAssetBuilder.Converted + "Play_Tree.prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static Texture2D Bake(GameObject source, Bounds bounds, float span, string path)
        {
            // An isolated preview scene and unlit copies bake albedo/coverage without
            // the site's lights, fog, sky, or Building Blocks entering the image.
            var scene = EditorSceneManager.NewPreviewScene(); var materials = new List<Material>();
            var target = RenderTexture.GetTemporary(BakeSize, BakeSize, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var image = new Texture2D(BakeSize, BakeSize, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                var model = Object.Instantiate(source); SceneManager.MoveGameObjectToScene(model, scene);
                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? throw new InvalidOperationException("Missing tree bake shader");
                foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    var replacements = new List<Material>();
                    foreach (var original in renderer.sharedMaterials)
                    {
                        var material = new Material(shader); materials.Add(material); replacements.Add(material);
                        material.SetTexture("_BaseMap", original.GetTexture("_BaseMap")); material.SetColor("_BaseColor", original.GetColor("_BaseColor"));
                        material.SetTextureScale("_BaseMap", original.GetTextureScale("_BaseMap")); material.SetTextureOffset("_BaseMap", original.GetTextureOffset("_BaseMap"));
                        material.SetFloat("_Cull", original.GetFloat("_Cull"));
                        material.SetFloat("_AlphaClip", 1); material.SetFloat("_Cutoff", original.GetFloat("_Cutoff")); material.EnableKeyword("_ALPHATEST_ON");
                    }
                    renderer.sharedMaterials = replacements.ToArray(); renderer.shadowCastingMode = ShadowCastingMode.Off;
                }
                var cameraObject = new GameObject("TreeBakeCamera"); SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false; camera.scene = scene;
                camera.orthographic = true; camera.orthographicSize = span / 2; camera.aspect = 1;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
                camera.allowHDR = false; camera.allowMSAA = false; camera.targetTexture = target;
                camera.transform.position = bounds.center - Vector3.forward * (bounds.size.z + span);
                camera.nearClipPlane = 0.01f; camera.farClipPlane = 2 * (bounds.size.z + span);
                camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, BakeSize, BakeSize), 0, 0); image.Apply();
                var pixels = image.GetPixels();
                if (!Array.Exists(pixels, p => p.a > 0.5f) || !Array.Exists(pixels, p => p.a < 0.5f))
                    throw new InvalidOperationException("WO-L4 tree bake must contain both foliage and transparent background; no image saved.");
                // Keep transparent padding identical at both edges, including RGB.
                for (var i = 0; i < pixels.Length; i++) if (pixels[i].a == 0) pixels[i] = Color.clear;
                return SiteTextureBuilder.Write(path, pixels, BakeSize, true, false);
            }
            finally
            {
                RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); Object.DestroyImmediate(image);
                EditorSceneManager.ClosePreviewScene(scene);
                foreach (var material in materials) Object.DestroyImmediate(material);
            }
        }
    }
}
