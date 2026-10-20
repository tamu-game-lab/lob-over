using System;
using LOBOver.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LOBOver.VR
{
    public sealed class ControllerPointer : MonoBehaviour
    {
        public Transform rightHand;
        public Camera mouseCamera;
        public WorldAssets assets;
        public IInspectorInput Input { get; set; } = new OvrInput();
        public event Action<RaycastHit> TargetPressed;
        public event Action AnyPress;
        private LineRenderer line;
        public Ray PointingRay => new Ray(rightHand.position, rightHand.forward);
        private void Start()
        {
            line = WorldDrawing.Line("ControllerRay", transform, assets.ink, Color.white, 0.001f);
            line.useWorldSpace = true; line.positionCount = 2;
        }
        private void Update()
        {
            var ray = PointingRay; var pressed = Input.TriggerDown;
#if UNITY_EDITOR
            if (mouseCamera != null && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            { ray = mouseCamera.ScreenPointToRay(Mouse.current.position.ReadValue()); pressed = true; }
#endif
            var hitSomething = Physics.Raycast(ray, out var hit, 10, ~0, QueryTriggerInteraction.Collide);
            if (line != null)
            { line.SetPosition(0, ray.origin); line.SetPosition(1, hitSomething ? hit.point : ray.GetPoint(3)); }
            if (!pressed) return;
            AnyPress?.Invoke();
            if (!hitSomething) return;
            var button = hit.collider.GetComponentInParent<RayButton>();
            if (button != null) button.Press(); else TargetPressed?.Invoke(hit);
        }
    }
}
