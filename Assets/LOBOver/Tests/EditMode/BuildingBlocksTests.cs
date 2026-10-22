using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using LOBOver.Editor;
using LOBOver.Play;
using LOBOver.XR;
using Meta.XR.BuildingBlocks;
using NUnit.Framework;
using Oculus.Interaction;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace LOBOver.Tests.Editor
{
    public sealed class BuildingBlocksTests
    {
        [Test]
        public void ExistingPlayHasEveryRequiredBlock()
        {
            var scene = PlaySceneBuilder.OpenExisting();
            Assert.That(BuildingBlocksSetup.Missing(scene), Is.Empty);
        }

        [Test]
        public void RebuildTwicePreservesBlocksRigAndUnownedRoots()
        {
            var scene = PlaySceneBuilder.OpenExisting();
            var sentinel = new GameObject("Unowned Test Sentinel");
            SceneManager.MoveGameObjectToScene(sentinel, scene);
            var protectedTransforms = XRRig.Components<BuildingBlock>(scene).SelectMany(block => block.GetComponentsInChildren<Transform>(true))
                .Concat(XRRig.Components<OVRCameraRig>(scene).SelectMany(rig => rig.GetComponentsInChildren<Transform>(true)))
                .Append(sentinel.transform).Distinct().ToArray();
            var before = protectedTransforms.Select(t => (t, id: t.GetInstanceID(), parent: t.parent, position: t.localPosition,
                rotation: t.localRotation, scale: t.localScale, active: t.gameObject.activeSelf)).ToArray();
            try
            {
                PlaySceneBuilder.Build(false); PlaySceneBuilder.Build(false);
                foreach (var item in before)
                {
                    Assert.That(item.t, Is.Not.Null);
                    Assert.That(item.t.GetInstanceID(), Is.EqualTo(item.id)); Assert.That(item.t.parent, Is.EqualTo(item.parent));
                    Assert.That(item.t.localPosition, Is.EqualTo(item.position)); Assert.That(item.t.localRotation, Is.EqualTo(item.rotation));
                    Assert.That(item.t.localScale, Is.EqualTo(item.scale)); Assert.That(item.t.gameObject.activeSelf, Is.EqualTo(item.active));
                }
                var roots = scene.GetRootGameObjects();
                foreach (var name in new[] { "Lighting", "Client", "Hub", "Site", "Warp", "Preview" })
                    Assert.That(roots.Count(root => root.name == "[LOBOver] " + name), Is.EqualTo(1));
                var floor = roots.Single(root => root.name == "[LOBOver] Hub").transform.Find("TrackedFloor").GetComponent<BoxCollider>();
                Assert.That(floor.bounds.size.x, Is.EqualTo(20)); Assert.That(floor.bounds.size.z, Is.EqualTo(20));
                Assert.That(floor.bounds.max.y, Is.EqualTo(0).Within(0.0001));
                var start = XRRig.Components<HubPanel>(scene).Single();
                foreach (var button in start.panel.GetComponentsInChildren<CrewButton>())
                {
                    Assert.That(button.GetComponent<RayInteractable>(), Is.Not.Null);
                    Assert.That(button.GetComponent<PokeInteractable>(), Is.Not.Null);
                    Assert.That(button.transform.Find("Face").localScale.x, Is.GreaterThanOrEqualTo(0.11f));
                    Assert.That(button.transform.Find("Face").localScale.y, Is.GreaterThanOrEqualTo(0.06f));
                }
                Assert.That(XRRig.Components<LOBOver.Runtime.Director>(scene), Is.Empty);
                Assert.That(XRRig.Components<LOBOver.World.PlanPanel>(scene), Is.Empty);
                Assert.That(start.panel.GetComponentInParent<OVRCameraRig>(), Is.Null);
                var preview = XRRig.Components<PlayPreview>(scene).Single();
                var eye = XRRig.Components<OVRCameraRig>(scene).Single().centerEyeAnchor.GetComponent<Camera>();
                Assert.That(preview.view.transform.parent, Is.EqualTo(eye.transform));
                Assert.That(preview.view.transform.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(preview.view.transform.localRotation, Is.EqualTo(Quaternion.identity));
                Assert.That(preview.view.fieldOfView, Is.EqualTo(eye.fieldOfView));
                Assert.That(preview.view.nearClipPlane, Is.EqualTo(eye.nearClipPlane));
                Assert.That(preview.view.farClipPlane, Is.EqualTo(eye.farClipPlane));
                Assert.That(preview.view.CompareTag("EditorOnly"), Is.True);
                Assert.That(XRRig.Components<Camera>(scene).Count(c => c.name == "[LOBOver] HeadPreviewCamera"), Is.EqualTo(1));
                Assert.That(preview.survey.name, Is.EqualTo("SurveyCamera"));
                Assert.That(preview.survey.enabled, Is.False);
            }
            finally { if (sentinel != null) UnityEngine.Object.DestroyImmediate(sentinel); }
        }

        [Test]
        public void ConfigureIsIdempotentAndDisablesLocomotion()
        {
            // Open committed bytes separately so another builder test cannot hide an initial Configure change.
            var scene = EditorSceneManager.OpenPreviewScene(PlaySceneBuilder.ScenePath);
            try
            {
                var objects = XRRig.Components<Component>(scene).Where(c => c != null).Cast<UnityEngine.Object>()
                    .Concat(XRRig.Components<Transform>(scene).Select(t => (UnityEngine.Object)t.gameObject)).ToArray();
                var before = objects.Select(EditorJsonUtility.ToJson).ToArray();
                var dirty = scene.isDirty;
                Assert.That(BuildingBlocksSetup.Check(scene), Is.True);
                for (var pass = 0; pass < 2; pass++)
                {
                    BuildingBlocksSetup.Configure(scene);
                    CollectionAssert.AreEqual(before, objects.Select(EditorJsonUtility.ToJson).ToArray());
                    Assert.That(scene.isDirty, Is.EqualTo(dirty));
                }
                foreach (var tunnel in XRRig.Components<TunnelingEffect>(scene)) Assert.That(tunnel.gameObject.activeSelf, Is.False);
                foreach (var behaviour in XRRig.Components<Behaviour>(scene).Where(c => c != null && c.GetType().Namespace == "Oculus.Interaction.Locomotion"))
                    Assert.That(behaviour.enabled, Is.False, behaviour.name);
                foreach (var locomotor in XRRig.Components<Oculus.Interaction.Locomotion.FirstPersonLocomotor>(scene))
                    Assert.That(locomotor.GravityFactor, Is.Zero);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void ActiveTunnelsFailCheckAndConfigureOnlyChangesTheirActiveValues()
        {
            var scene = EditorSceneManager.OpenPreviewScene(PlaySceneBuilder.ScenePath);
            try
            {
                var tunnels = XRRig.Components<TunnelingEffect>(scene);
                Assert.That(tunnels.Length, Is.EqualTo(2), "Exercise both installed SDK tunnel objects");
                var components = XRRig.Components<Component>(scene).Where(c => c != null).ToArray();
                var before = components.Select(EditorJsonUtility.ToJson).ToArray();
                foreach (var tunnel in tunnels)
                {
                    tunnel.gameObject.SetActive(true);
                    LogAssert.Expect(LogType.Error, new Regex("active TunnelingEffect .*" + Regex.Escape(tunnel.name)));
                }
                Assert.That(BuildingBlocksSetup.Check(scene), Is.False);
                BuildingBlocksSetup.Configure(scene);
                Assert.That(BuildingBlocksSetup.Check(scene), Is.True);
                foreach (var tunnel in tunnels) Assert.That(tunnel.gameObject.activeSelf, Is.False);
                CollectionAssert.AreEqual(components, XRRig.Components<Component>(scene).Where(c => c != null).ToArray());
                CollectionAssert.AreEqual(before, components.Select(EditorJsonUtility.ToJson).ToArray());
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void CheckFindsUnownedTunnelsEvenWithDisabledDriverButIgnoresHiddenRenderers()
        {
            var scene = EditorSceneManager.OpenPreviewScene(PlaySceneBuilder.ScenePath);
            try
            {
                var obj = new GameObject("Unowned tunnel"); SceneManager.MoveGameObjectToScene(obj, scene);
                var tunnel = obj.AddComponent<TunnelingEffect>(); tunnel.enabled = false;
                var mesh = new GameObject("Mesh"); mesh.transform.SetParent(obj.transform, false);
                var renderer = mesh.AddComponent<MeshRenderer>();
                LogAssert.Expect(LogType.Error, new Regex("active TunnelingEffect Unowned tunnel;"));
                Assert.That(BuildingBlocksSetup.Check(scene), Is.False);
                renderer.enabled = false; Assert.That(BuildingBlocksSetup.Check(scene), Is.True);
                renderer.enabled = true; mesh.SetActive(false); Assert.That(BuildingBlocksSetup.Check(scene), Is.True);
                mesh.SetActive(true);
                BuildingBlocksSetup.Configure(scene);
                Assert.That(obj.activeSelf, Is.False, "Configure must reach tunnels outside Building Blocks");
                Assert.That(BuildingBlocksSetup.Check(scene), Is.True);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void SourcesNeverBuildRigsOrRaycastWithControllerInput()
        {
            foreach (var folder in new[] { "Editor", "Runtime" })
                foreach (var file in Directory.GetFiles("Assets/LOBOver/" + folder, "*.cs", SearchOption.AllDirectories))
                {
                    var source = File.ReadAllText(file);
                    Assert.That(Regex.IsMatch(source, @"AddComponent\s*<\s*" + "OVR"), Is.False, file);
                    Assert.That(Regex.IsMatch(source, @"Instantiate\s*\([^;]*(?:" + "OVRCameraRig|rig.gameObject)"), Is.False, file);
                    Assert.That(source.Contains("OVRInput.") && source.Contains("Physics.Raycast"), Is.False, file);
                }
            Assert.That(File.Exists("Assets/LOBOver/Runtime/VR/" + "ControllerPointer.cs"), Is.False);
        }
    }
}
