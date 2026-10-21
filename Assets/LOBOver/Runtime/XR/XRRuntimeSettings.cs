using System.Collections;
using UnityEngine;

namespace LOBOver.XR
{
    public sealed class XRRuntimeSettings : MonoBehaviour
    {
        private const float TargetRefreshRate = 72; // WO-S4 performance target, retained by WO-S6.
        private IEnumerator Start()
        {
            // WO-S4 performance settings, centralized by WO-S6. No scene OVRManager fields.
            while (!OVRManager.isHmdPresent || OVRManager.display == null) yield return null;
            OVRManager.display.displayFrequency = TargetRefreshRate;
            OVRManager.useDynamicFoveatedRendering = false;
            OVRManager.foveatedRenderingLevel = OVRManager.FoveatedRenderingLevel.Medium;
        }
    }
}
