using System.Collections.Generic;
using System.Linq;
using Meta.XR.BuildingBlocks;
using Oculus.Interaction;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

namespace LOBOver.XR
{
    // Read-only access to Bryan's blocks. Nothing here configures or moves their objects.
    [DefaultExecutionOrder(-600)]
    public sealed class XRRig : MonoBehaviour
    {
        public OVRCameraRig CameraRig { get; private set; }
        public Transform CentreEye => CameraRig == null ? null : CameraRig.centerEyeAnchor;
        public Transform editorHead;
        public Transform ViewEye
        {
            get
            {
#if UNITY_EDITOR
                if (!OVRManager.isHmdPresent && editorHead != null) return editorHead;
#endif
                return CentreEye;
            }
        }
        public Transform LeftController => CameraRig == null ? null : CameraRig.leftControllerAnchor;
        public Transform RightController => CameraRig == null ? null : CameraRig.rightControllerAnchor;
        public Transform LeftHand => CameraRig == null ? null : CameraRig.leftHandAnchor;
        public Transform RightHand => CameraRig == null ? null : CameraRig.rightHandAnchor;
        public Transform LeftWrist => leftTrackedHand != null && leftTrackedHand.IsTracked ? LeftHand : LeftController;
        public OVRPassthroughLayer Passthrough { get; private set; }
        public RayInteractor[] Rays { get; private set; }
        public PokeInteractor[] Pokes { get; private set; }
        private readonly HashSet<string> reported = new HashSet<string>();
        private OVRHand leftTrackedHand;
        // Meta SDK's Real Hands block id (core/Editor/BuildingBlocks/BlockDataIds.cs).
        public const string RealHandsId = "f547fe18-d477-46ec-bdf5-7208df19cb98";

        public static T[] Components<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
        private void Awake() => Resolve(false); private void Start() => Resolve(); // OVRCameraRig fills its anchors in its own Awake, so validate once all Awakes have run
        public bool Resolve(bool logErrors = true)
        {
            var scene = gameObject.scene;
            CameraRig = Components<OVRCameraRig>(scene).FirstOrDefault();
            var blocks = Components<BuildingBlock>(scene);
            var hands = Components<OVRHand>(scene);
            leftTrackedHand = hands.FirstOrDefault(hand => hand.GetHand() == OVRPlugin.Hand.HandLeft);
            // These block ids cover both the core and comprehensive ISDK tracking installations.
            var controllerTracking = blocks.Any(block => block.BlockId == "5817f7c0-f2a5-45f9-a5ca-64264e0166e8"
                || block.BlockId == "f10154e0-16b2-492f-97d0-6639f69e7df6")
                || Components<Oculus.Interaction.Input.FromOVRControllerDataSource>(scene).Length >= 2;
            var handTracking = blocks.Any(block => block.BlockId == "8b26b298-7bf4-490e-b245-a039c0184303"
                || block.BlockId == "0393ca30-f2a9-4865-a40f-f9a68d01c3a9")
                || Components<Oculus.Interaction.Input.FromOVRHandDataSource>(scene).Length >= 2;
            // Prefer the explicit Passthrough block; the additional layer on the rig is preserved.
            var layers = Components<OVRPassthroughLayer>(scene);
            Passthrough = layers.FirstOrDefault(layer => layer.GetComponent<BuildingBlock>() != null
                && layer.GetComponent<OVRCameraRig>() == null) ?? layers.FirstOrDefault();
            Rays = Components<RayInteractor>(scene); Pokes = Components<PokeInteractor>(scene);
            var valid = Require(CameraRig != null && CentreEye != null, "Camera Rig (centre eye)", logErrors);
            valid &= Require(controllerTracking && LeftController != null, "Controller Tracking (left)", logErrors);
            valid &= Require(controllerTracking && RightController != null, "Controller Tracking (right)", logErrors);
            valid &= Require(handTracking && LeftHand != null, "Hand Tracking (left)", logErrors);
            valid &= Require(handTracking && RightHand != null, "Hand Tracking (right)", logErrors);
            valid &= Require(Passthrough != null, "Passthrough", logErrors);
            valid &= Require(Rays.Length > 0 && Pokes.Length > 0, "Interactions Rig (ray and poke)", logErrors);
            return valid;
        }
        private bool Require(bool present, string block, bool log)
        {
            if (!present && log && reported.Add(block)) Debug.LogError("LOB Over: missing " + block + "; add it from Building Blocks.", this);
            return present;
        }
        public bool TryHeadPose(out Pose pose)
        {
            pose = default;
            if (CentreEye == null) return false;
#if UNITY_EDITOR
            if (!OVRManager.isHmdPresent)
            {
                pose = new Pose(ViewEye.position, ViewEye.rotation);
                return true;
            }
#endif
            var device = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (!device.isValid || !device.TryGetFeatureValue(CommonUsages.isTracked, out var tracked) || !tracked) return false;
            var position = CentreEye.position;
            if (float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsNaN(position.z)) return false;
            pose = new Pose(position, CentreEye.rotation); return true;
        }
    }
}
