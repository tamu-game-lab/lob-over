using System.Collections;
using System.Collections.Generic;
using LOBOver.Client;
using LOBOver.XR;
using TMPro;
using UnityEngine;
using UnityEngine.XR;

namespace LOBOver.Play
{
    public sealed class HubPanel : MonoBehaviour
    {
        public HostConnection connection;
        public XRRig rig;
        public WarpController warp;
        public Transform panel;
        public TextMeshPro status;
        public bool Placed { get; private set; }
        // WO-L2 hub dimensions and first-pose placement, in metres.
        public const float Width = 0.40f, Ahead = 0.55f, BelowEye = 0.15f;
        public const float TitleHeight = 0.025f, TextHeight = 0.015f;
        public const float ButtonWidth = 0.36f, ButtonHeight = 0.06f;
        private readonly List<XRInputSubsystem> inputs = new List<XRInputSubsystem>();
        private OVRDisplay display;
        private bool placementPending;
        private void OnEnable()
        {
            connection.Changed += Refresh;
            if (warp != null) warp.Changed += Refresh;
            SubsystemManager.GetSubsystems(inputs);
            foreach (var input in inputs) input.trackingOriginUpdated += TrackingOriginUpdated;
            placementPending = !Placed;
        }
        private IEnumerator Start()
        {
            panel.gameObject.SetActive(false);
            Refresh();
            while (!Placed) { TryPlace(); yield return null; }
        }
        private void Update()
        {
            if (display != OVRManager.display)
            {
                if (display != null) display.RecenteredPose -= Recenter;
                display = OVRManager.display;
                if (display != null) display.RecenteredPose += Recenter;
            }
            if (placementPending) TryPlace();
        }
        private void OnDisable()
        {
            connection.Changed -= Refresh;
            if (warp != null) warp.Changed -= Refresh;
            foreach (var input in inputs) input.trackingOriginUpdated -= TrackingOriginUpdated;
            if (display != null) display.RecenteredPose -= Recenter;
            display = null;
        }
        private void TrackingOriginUpdated(XRInputSubsystem ignored) => Recenter();
        public void Recenter() => placementPending = true;
        private void TryPlace()
        {
            if (rig == null || !rig.TryHeadPose(out var pose)) return;
            var forward = Vector3.ProjectOnPlane(pose.rotation * Vector3.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.1f) forward = Vector3.forward;
            var position = pose.position + forward * Ahead - Vector3.up * BelowEye;
            panel.SetPositionAndRotation(position, Quaternion.LookRotation(position - pose.position));
            panel.gameObject.SetActive(true); Placed = true; placementPending = false;
        }
        private void Refresh()
        {
            status.text = warp != null && warp.Busy ? "Warping..."
                : !connection.Connected ? "Looking for the game. Start it on the PC with Play LOB Over."
                : !connection.Started ? "Connected to " + connection.Hostname + ". Waiting for Plan to press Start."
                : "Day " + connection.DisplayDay;
        }
    }
}
