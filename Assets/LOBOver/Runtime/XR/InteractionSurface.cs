using System;
using Oculus.Interaction;
using Oculus.Interaction.Surfaces;
using UnityEngine;

namespace LOBOver.XR
{
    public sealed class InteractionSurface : MonoBehaviour
    {
        public event Action<Vector3> Selected;
        private RayInteractable ray;
        private PokeInteractable poke;
        private void SelectedEvent(PointerEvent evt)
        {
            if (evt.Type == PointerEventType.Select) Selected?.Invoke(evt.Pose.position);
        }
        public static InteractionSurface ForCollider(Collider collider)
        {
            var existing = collider.GetComponent<InteractionSurface>();
            if (existing != null) return existing;
            var surface = collider.gameObject.AddComponent<ColliderSurface>(); surface.InjectCollider(collider);
            var bridge = collider.gameObject.AddComponent<InteractionSurface>();
            bridge.ray = collider.gameObject.AddComponent<RayInteractable>(); bridge.ray.InjectSurface(surface);
            bridge.ray.WhenPointerEventRaised += bridge.SelectedEvent;
            return bridge;
        }
        public static void Canvas(Canvas canvas)
        {
            var pointable = canvas.gameObject.AddComponent<PointableCanvas>(); pointable.InjectCanvas(canvas);
            Plane(canvas.gameObject, ((RectTransform)canvas.transform).rect.size, pointable, out _, out _);
        }
        public static InteractionSurface Button(GameObject obj, Vector2 size)
        {
            var bridge = obj.AddComponent<InteractionSurface>();
            Plane(obj, size, null, out bridge.ray, out bridge.poke);
            bridge.ray.WhenPointerEventRaised += bridge.SelectedEvent;
            bridge.poke.WhenPointerEventRaised += bridge.SelectedEvent;
            return bridge;
        }
        private static void Plane(GameObject obj, Vector2 size, IPointableElement pointable, out RayInteractable ray, out PokeInteractable poke)
        {
            var plane = obj.AddComponent<PlaneSurface>(); plane.InjectNormalFacing(PlaneSurface.NormalFacing.Backward);
            var bounds = obj.AddComponent<BoundsClipper>(); bounds.Size = new Vector3(size.x, size.y, 0.01f);
            var surface = obj.AddComponent<ClippedPlaneSurface>(); surface.InjectAllClippedPlaneSurface(plane, new[] { bounds });
            ray = obj.AddComponent<RayInteractable>(); ray.InjectSurface(surface);
            poke = obj.AddComponent<PokeInteractable>(); poke.InjectSurfacePatch(surface);
            if (pointable != null) { ray.InjectOptionalPointableElement(pointable); poke.InjectOptionalPointableElement(pointable); }
        }
        private void OnDestroy()
        {
            if (ray != null) ray.WhenPointerEventRaised -= SelectedEvent;
            if (poke != null) poke.WhenPointerEventRaised -= SelectedEvent;
        }
    }
}
