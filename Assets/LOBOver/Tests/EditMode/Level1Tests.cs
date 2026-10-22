using System.Linq;
using System.Text.RegularExpressions;
using LOBOver.Client;
using LOBOver.Editor;
using LOBOver.XR;
using Meta.XR.BuildingBlocks;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace LOBOver.Tests.Editor
{
    public sealed class Level1Tests
    {
        [TestCase("LOBOVER 1 7331 BRYAN-PC", 7331, "BRYAN-PC")]
        [TestCase("LOBOVER 1 7333 test-host\n", 7333, "test-host")]
        public void DiscoveryReadsVersionPortAndHostname(string packet, int expected, string name)
        {
            Assert.That(DiscoveryPacket.TryParse(packet, out var port, out var host), Is.True);
            Assert.That(port, Is.EqualTo(expected)); Assert.That(host, Is.EqualTo(name));
        }
        [TestCase("LOBOVER 2 7331 host")]
        [TestCase("LOBOVER 1 0 host")]
        [TestCase("LOBOVER 1 65536 host")]
        [TestCase("LOBOVER 1 7331 host extra")]
        [TestCase("not a discovery packet")]
        public void DiscoveryRejectsInvalidPackets(string packet) => Assert.That(DiscoveryPacket.TryParse(packet, out _, out _), Is.False);

        [TestCase(true)]
        [TestCase(false)]
        public void CheckFailsForRealHandsOrTwoPassthroughLayers(bool realHands)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                var root = new GameObject("Test blocks"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
                if (realHands)
                {
                    var block = root.AddComponent<BuildingBlock>();
                    using var serialized = new SerializedObject(block);
                    var property = serialized.FindProperty("blockId");
                    Assert.That(property, Is.Not.Null, "SDK BuildingBlock id field changed");
                    property.stringValue = XRRig.RealHandsId; serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                else
                {
                    root.AddComponent<OVRPassthroughLayer>();
                    var extra = new GameObject("Extra passthrough"); extra.transform.SetParent(root.transform);
                    extra.AddComponent<OVRPassthroughLayer>();
                }
                foreach (var name in BuildingBlocksSetup.Missing(scene))
                    LogAssert.Expect(LogType.Error, "LOB Over: MISSING " + name + "; add it from Building Blocks.");
                foreach (var name in BuildingBlocksSetup.Forbidden(scene))
                    LogAssert.Expect(LogType.Error, "LOB Over: remove " + name + " in the Building Blocks window before building Play.unity.");
                Assert.That(BuildingBlocksSetup.Check(scene), Is.False);
                Assert.That(BuildingBlocksSetup.Forbidden(scene).Any(n => n.Contains(realHands ? "Real Hands" : "extra Passthrough")), Is.True);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        [Test]
        public void WarpShadersImportWithoutErrors()
        {
            foreach (var path in new[] { "SiteLit", "SiteSky" })
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/LOBOver/Art/" + path + ".shader");
                Assert.That(shader, Is.Not.Null);
                Assert.That(ShaderUtil.ShaderHasError(shader), Is.False, path);
            }
        }
        [Test]
        public void ConfiguredEyeStartsTransparentWithoutHdr()
        {
            var scene = PlaySceneBuilder.OpenExisting(); BuildingBlocksSetup.Configure(scene);
            var camera = XRRig.Components<OVRCameraRig>(scene).Single().centerEyeAnchor.GetComponent<Camera>();
            Assert.That(camera.clearFlags, Is.EqualTo(CameraClearFlags.SolidColor));
            Assert.That(camera.backgroundColor.a, Is.Zero); Assert.That(camera.allowHDR, Is.False);
            var passthrough = XRRig.Components<OVRPassthroughLayer>(scene).Single();
            Assert.That(passthrough.enabled, Is.True); Assert.That(passthrough.hidden, Is.False);
        }
    }
}
