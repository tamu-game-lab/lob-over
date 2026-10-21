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
            }
            finally { if (sentinel != null) UnityEngine.Object.DestroyImmediate(sentinel); }
        }

        [Test]
        public void ConfigureIsIdempotentAndDisablesLocomotion()
        {
            var scene = PlaySceneBuilder.OpenExisting();
            BuildingBlocksSetup.Configure(scene);
            var components = XRRig.Components<Component>(scene).Where(c => c != null).ToArray();
            var before = components.Select(UnityEditor.EditorJsonUtility.ToJson).ToArray();
            BuildingBlocksSetup.Configure(scene);
            CollectionAssert.AreEqual(before, components.Select(UnityEditor.EditorJsonUtility.ToJson).ToArray());
            foreach (var behaviour in XRRig.Components<Behaviour>(scene).Where(c => c.GetType().Namespace == "Oculus.Interaction.Locomotion"))
                Assert.That(behaviour.enabled, Is.False, behaviour.name);
            foreach (var locomotor in XRRig.Components<Oculus.Interaction.Locomotion.FirstPersonLocomotor>(scene))
                Assert.That(locomotor.GravityFactor, Is.Zero);
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
