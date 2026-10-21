using UnityEngine;

namespace LOBOver.XR
{
    // Only owned presentation objects follow anchors; the block hierarchy never changes.
    public sealed class RigAnchorFollower : MonoBehaviour
    {
        public XRRig rig;
        public bool leftWrist;
        public Vector3 positionOffset, rotationOffset;
        private void LateUpdate()
        {
            if (rig == null) return;
            var anchor = leftWrist ? rig.LeftWrist : rig.CentreEye;
            if (anchor == null) return;
            transform.SetPositionAndRotation(anchor.TransformPoint(positionOffset), anchor.rotation * Quaternion.Euler(rotationOffset));
        }
    }
}
