using System.IO;
using LOBOver.MR;
using LOBOver.World;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace LOBOver.Tests
{
    public sealed class DioramaScaleTests
    {
        [TestCase("slice_texas", 2f)]
        [TestCase("stage1_texas", 4f)]
        [TestCase("stage2_texas", 4f)]
        public void AuthoredJobFitsTable(string scenario, float expectedMetres)
        {
#if UNITY_EDITOR
            var source = JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath, "../Data/Scenarios/" + scenario + ".json")));
            var length = (double)source["job"]["lengthFt"];
            var root = new GameObject("DioramaRoot");
            try
            {
                root.transform.localScale = Vector3.one * DioramaSeat.ScaleFor(length);
                var far = root.transform.TransformPoint(Vector3.right * (float)length * WorldUnits.MetresPerFoot);
                Assert.That(Vector3.Distance(root.transform.position, far), Is.EqualTo(expectedMetres).Within(0.01f));
            }
            finally { Object.DestroyImmediate(root); }
#else
            Assert.Ignore("Authored scenario fixture is read in Editor PlayMode.");
#endif
        }
    }
}
