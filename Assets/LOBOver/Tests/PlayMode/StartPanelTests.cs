using LOBOver.Start;
using NUnit.Framework;
using UnityEngine;

namespace LOBOver.Tests
{
    public sealed class StartPanelTests
    {
        private GameObject root;
        private StartPanel panel;
        [SetUp]
        public void Setup()
        {
            root = new GameObject("ModeTest"); panel = root.AddComponent<StartPanel>();
            panel.fullScaleRoot = Child("Full"); panel.vrRoot = Child("VR"); panel.mrRoot = Child("MR"); panel.desktopRoot = Child("Desktop");
            panel.fullScalePlan = Child("Plan"); panel.fullScalePlan.transform.SetParent(panel.fullScaleRoot.transform);
            panel.panel = Child("Menu").transform;
        }
        private GameObject Child(string name) { var obj = new GameObject(name); obj.transform.SetParent(root.transform); return obj; }
        [TearDown] public void Cleanup() => Object.DestroyImmediate(root);
        [TestCase(ShowcaseMode.VRSite, true, true, false, false)]
        [TestCase(ShowcaseMode.MRDiorama, false, false, true, false)]
        [TestCase(ShowcaseMode.Desktop, true, false, false, true)]
        public void OnlySelectedModeAndItsSharedWorldAreActive(ShowcaseMode mode, bool full, bool vr, bool mr, bool desktop)
        {
            // Traverse all modes first: a prior active root must also be switched off.
            panel.ActivateMode(ShowcaseMode.MRDiorama); panel.ActivateMode(ShowcaseMode.Desktop); panel.ActivateMode(mode);
            Assert.That(panel.fullScaleRoot.activeSelf, Is.EqualTo(full)); Assert.That(panel.vrRoot.activeSelf, Is.EqualTo(vr));
            Assert.That(panel.mrRoot.activeSelf, Is.EqualTo(mr)); Assert.That(panel.desktopRoot.activeSelf, Is.EqualTo(desktop));
            Assert.That(panel.fullScalePlan.activeInHierarchy, Is.EqualTo(desktop)); Assert.That(panel.panel.gameObject.activeSelf, Is.False);
        }
    }
}
