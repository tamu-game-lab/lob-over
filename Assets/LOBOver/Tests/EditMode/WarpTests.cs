using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using LOBOver.Editor;
using LOBOver.Play;
using NUnit.Framework;
using UnityEngine;

namespace LOBOver.Tests.Editor
{
    public sealed class WarpTests
    {
        [Test]
        public void StaticMeshCornersHaveUnitBarycentricsAndKeepThemOnRebuild()
        {
            // The road is part of GroundRelief. These are all MeshData-generated surfaces.
            var meshes = new[] { SiteGeometryBuilder.Ground(), SiteGeometryBuilder.HorizonMesh(), SiteGeometryBuilder.Strip(),
                SiteGeometryBuilder.Wall(1), SiteGeometryBuilder.Wall(-1), SiteGeometryBuilder.Collapse(),
                SiteGeometryBuilder.Windrow(), SiteGeometryBuilder.HeroPile(), SiteGeometryBuilder.Pipe(), SiteGeometryBuilder.Block() };
            foreach (var mesh in meshes)
            {
                var barycentrics = new List<Vector4>(); mesh.GetUVs(2, barycentrics);
                Assert.That(barycentrics.Count, Is.EqualTo(mesh.vertexCount), mesh.name);
                Assert.That(mesh.triangles.Distinct().Count(), Is.EqualTo(mesh.vertexCount), mesh.name + " must split triangle corners");
                foreach (var barycentric in barycentrics)
                {
                    Assert.That(barycentric.x + barycentric.y + barycentric.z, Is.EqualTo(1).Within(0.00001), mesh.name);
                    Assert.That(barycentric.w, Is.EqualTo(1), mesh.name + " builder marker");
                }
                var triangles = mesh.triangles;
                for (var i = 0; i < triangles.Length; i += 3)
                {
                    var sum = barycentrics[triangles[i]] + barycentrics[triangles[i + 1]] + barycentrics[triangles[i + 2]];
                    Assert.That(sum, Is.EqualTo(new Vector4(1, 1, 1, 3)), mesh.name);
                }
                var before = barycentrics.ToArray();
                var copy = Object.Instantiate(mesh);
                try
                {
                    SiteGeometryBuilder.BakeBarycentrics(copy); copy.GetUVs(2, barycentrics);
                    CollectionAssert.AreEqual(before, barycentrics, mesh.name + " rebake");
                }
                finally { Object.DestroyImmediate(copy); }
            }
        }
        [Test]
        public void PlaySceneContainsNoRemovedTransitionRendererOrShader()
        {
            var scene = PlaySceneBuilder.Build(false);
            foreach (var transform in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)))
                Assert.That(transform.name, Is.Not.EqualTo("WarpSphere"));
            foreach (var material in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Renderer>(true)).SelectMany(r => r.sharedMaterials))
            {
                Assert.That(material, Is.Not.Null);
                Assert.That(material.shader, Is.Not.Null, material.name);
                Assert.That(material.shader.name, Is.Not.EqualTo("LOBOver/Warp").And.Not.EqualTo("LOBOver/WarpFade"));
            }
            foreach (var shader in new[] { "Warp", "WarpFade" })
            {
                Assert.That(File.Exists("Assets/LOBOver/Art/" + shader + ".shader"), Is.False);
                Assert.That(File.Exists("Assets/LOBOver/Art/" + shader + ".mat"), Is.False);
            }
        }
        [Test]
        public void SourcesContainNoAlwaysDepthTestOrViewCoveringMesh()
        {
            var failures = new List<string>();
            // WO-L5 Amendment 1: only the legacy Site.unity inspector mask is exempt.
            // The amendment names Art/ComfortFade.shader; its tracked location is Runtime/VR.
            var legacyMask = new HashSet<string>
            {
                "Assets/LOBOver/Runtime/VR/ComfortFade.cs",
                "Assets/LOBOver/Runtime/VR/ComfortFade.shader"
            };
            foreach (var path in Directory.GetFiles("Assets/LOBOver", "*", SearchOption.AllDirectories)
                .Where(p => (p.EndsWith(".cs") || p.EndsWith(".shader")) && !p.Replace('\\', '/').Contains("/Tests/")
                    && !legacyMask.Contains(p.Replace('\\', '/'))))
            {
                var source = File.ReadAllText(path);
                if (Regex.IsMatch(source, @"ZTest\s+Always|WarpSphere|fullMesh|RideVignette")) failures.Add(path);
            }
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }
        [Test]
        public void CompositeUsesPremultipliedRgbExactlyOnce()
        {
            var room = new Color(0.5f, 0.5f, 0.5f, 1);
            Assert.That(PlayPreview.Composite(Color.clear, room, false), Is.EqualTo(room));
            Assert.That(PlayPreview.Composite(new Color(0.02f, 0.04f, 0.06f, 0.25f), room, false),
                Is.EqualTo(new Color(0.395f, 0.415f, 0.435f, 1)).Using(new ColorComparer()));
            var opaque = new Color(0.2f, 0.3f, 0.4f, 1);
            Assert.That(PlayPreview.Composite(opaque, room, false), Is.EqualTo(opaque));
            var linearRoom = PlayPreview.Composite(Color.clear, room, true);
            Assert.That(linearRoom.r, Is.EqualTo(room.r).Within(0.001));
        }
        private sealed class ColorComparer : IEqualityComparer<Color>
        {
            public bool Equals(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) + Mathf.Abs(a.a - b.a) < 0.00001f;
            public int GetHashCode(Color value) => value.GetHashCode();
        }
    }
}
