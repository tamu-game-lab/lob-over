using LOBOver.XR;
using Oculus.Interaction;
using UnityEngine;
#if UNITY_EDITOR
using UnityEngine.InputSystem;
#endif

namespace LOBOver.Play
{
    // The entire root is EditorOnly. Mouse clicks exercise the same ISDK event as a ray selection.
    public sealed class PlayPreview : MonoBehaviour
    {
        public Camera view;
        public XRRig rig;
        public CrewButton back;
        private void Start()
        {
#if UNITY_EDITOR
            if (OVRManager.isHmdPresent) gameObject.SetActive(false);
#else
            gameObject.SetActive(false);
#endif
        }
        private void Update()
        {
#if UNITY_EDITOR
            var mouse = Mouse.current; var keyboard = Keyboard.current;
            if (mouse == null) return;
            if (mouse.leftButton.wasPressedThisFrame && Physics.Raycast(view.ScreenPointToRay(mouse.position.ReadValue()), out var hit))
            {
                var button = hit.collider.GetComponentInParent<CrewButton>();
                if (button != null) Select(button);
            }
            if (keyboard != null && keyboard.bKey.wasPressedThisFrame && back.gameObject.activeInHierarchy) Select(back);
#endif
        }
        private static void Select(CrewButton button)
        {
            var ray = button.GetComponent<RayInteractable>();
            if (!button.Available || ray == null) return;
            var pose = new Pose(button.transform.position, button.transform.rotation);
            ray.PublishPointerEvent(new PointerEvent(-1, PointerEventType.Hover, pose));
            ray.PublishPointerEvent(new PointerEvent(-1, PointerEventType.Select, pose));
            ray.PublishPointerEvent(new PointerEvent(-1, PointerEventType.Unselect, pose));
            ray.PublishPointerEvent(new PointerEvent(-1, PointerEventType.Unhover, pose));
        }
    }
}
