using System.IO;
using System.Linq;
using LOBOver.Editor;
using LOBOver.Runtime;
using LOBOver.World;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace LOBOver.Tests.Editor
{
    // PlayMode's setup names this class by string so its assembly has no runtime dependency on UnityEditor.
    public sealed class BuildSiteFixture : IPrebuildSetup
    {
        public void Setup() { SiteSceneBuilder.Build(); }
    }

    public sealed class SiteSceneBuilderTests
    {
        [Test]
        public void BuilderIsRepeatableAndWiresTheScene()
        {
            SiteSceneBuilder.Build(false);
            var first = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().Select(o => o.name).OrderBy(n => n).ToArray();
            SiteSceneBuilder.Build(false);
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            CollectionAssert.AreEqual(first, scene.GetRootGameObjects().Select(o => o.name).OrderBy(n => n).ToArray());
            foreach (var name in new[] { "Sun", "Ground", "Road", "Gate", "Trailer", "JobSign", "Director", "SiteWorld", "DesktopPreview", "SiteSky", "Global Volume" })
                Assert.That(GameObject.Find(name), Is.Not.Null, name);
            Assert.That(Object.FindObjectsByType<Director>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<SiteWorld>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(GameObject.Find("Gate").transform.childCount, Is.EqualTo(5));
            Assert.That(GameObject.Find("Road").transform.localScale, Is.EqualTo(new Vector3(980, 0.03f, 8)));
            var world = Object.FindFirstObjectByType<SiteWorld>();
            Assert.That(world.director, Is.Not.Null); Assert.That(world.assets.machines.Length, Is.EqualTo(8));
            Assert.That(Vector3.Distance(world.trailerBoard.transform.Find("Face").lossyScale, new Vector3(2, 1, 0.06f)), Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance(world.planPanel.transform.Find("Face").lossyScale, new Vector3(3, 1.5f, 0.06f)), Is.LessThan(0.0001f));
            var source = JObject.Parse(File.ReadAllText("Data/Scenarios/slice_texas.json"));
            world.Configure((JObject)source["job"], (double)source["plan"]["deadlineDay"]);
            var transcript = JObject.Parse(File.ReadAllText("Data/transcripts/slice_play.json"));
            world.Apply(LOBOver.Sim.SiteView.From((JObject)transcript["snapshots"][1]["site"]), 2);
            Assert.That(world.CellCount, Is.EqualTo(30)); Assert.That(world.CrewCount, Is.EqualTo(3)); Assert.That(world.BoardCount, Is.EqualTo(2));
            Assert.That(world.StakeAt(3000), Is.Not.Null);
            Assert.That(world.transform.GetComponentsInChildren<Transform>().Count(t => t.name.StartsWith("Spoil_")), Is.EqualTo(30));
            foreach (var rig in world.GetComponentsInChildren<CrewRig>()) Assert.That(rig.GetComponentsInChildren<Animator>().Length, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void ImportedPathsAndCleanCopiesLoad()
        {
            var assets = SiteAssetBuilder.Build();
            foreach (var name in SiteAssetBuilder.MachineNames)
                Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(SiteAssetBuilder.Equipment + name + ".prefab"), Is.Not.Null, name);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(SiteAssetBuilder.Worker), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(SiteAssetBuilder.Pipe), Is.Not.Null);
            var prefabs = assets.machines.Select(m => m.prefab).Concat(new[] { assets.worker, assets.cellPrefab.gameObject, assets.particles, assets.spoil, assets.cone, assets.barrier });
            foreach (var prefab in prefabs)
                foreach (var child in prefab.GetComponentsInChildren<Transform>(true))
                    Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject), Is.Zero, prefab.name + "/" + child.name);
        }

        [Test]
        public void SyncCopiesExactCatalogScenariosScriptsAndVoice()
        {
            DataSync.Sync();
            Check("Data/catalog.json", "Data/catalog.json");
            foreach (var file in Directory.GetFiles("Data/Scenarios", "*.json")) Check(file, "Data/Scenarios/" + Path.GetFileName(file));
            foreach (var file in Directory.GetFiles("Monitor/test/scripts", "*.json")) Check(file, "Data/scripts/" + Path.GetFileName(file));
            foreach (var file in Directory.GetFiles("Voice", "*.wav", SearchOption.AllDirectories)) Check(file, file.Replace('\\', '/'));
        }
        private static void Check(string source, string relative) => CollectionAssert.AreEqual(File.ReadAllBytes(source), File.ReadAllBytes(Path.Combine(Application.streamingAssetsPath, relative)), source);
    }
}
