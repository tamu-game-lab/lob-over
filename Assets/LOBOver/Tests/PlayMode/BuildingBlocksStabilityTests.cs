using System.Collections;
using LOBOver.Start;
using LOBOver.XR;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LOBOver.Tests
{
    public sealed class BuildingBlocksStabilityTests
    {
        [UnityTest]
        public IEnumerator RigAndStartPanelStayStillForThreeSecondsWithoutHeadset()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/LOBOver/Scenes/Site.unity", new LoadSceneParameters(LoadSceneMode.Single));
            var start = Object.FindFirstObjectByType<StartPanel>();
            var rig = Object.FindFirstObjectByType<XRRig>();
            Assert.That(rig.CameraRig, Is.Not.Null);
            for (var frame = 0; frame < 120 && !start.Placed; frame++) yield return null;
            Assert.That(start.Placed, Is.True, "Start panel never received its first head pose");
            Assert.That(start.panel.gameObject.activeInHierarchy, Is.True);
            var y = rig.CameraRig.transform.position.y; var position = start.panel.position; var rotation = start.panel.rotation;
            Debug.Log("WO-S6 rig y at 0 s: " + y.ToString("F4"));
            var until = Time.realtimeSinceStartup + 3;
            while (Time.realtimeSinceStartup < until)
            {
                Assert.That(rig.CameraRig.transform.position.y, Is.EqualTo(y).Within(0.01));
                Assert.That(Vector3.Distance(start.panel.position, position), Is.LessThan(0.0001f));
                Assert.That(Quaternion.Angle(start.panel.rotation, rotation), Is.LessThan(0.001f));
                yield return null;
            }
            Debug.Log("WO-S6 rig y at 3 s: " + rig.CameraRig.transform.position.y.ToString("F4"));
            start.PlaceOnce(new Pose(position + Vector3.one, Quaternion.Euler(0, 90, 0)));
            Assert.That(start.panel.position, Is.EqualTo(position), "A later pose moved the placed panel");
#else
            Assert.Ignore("Editor no-headset stability check"); yield break;
#endif
        }
    }
}
