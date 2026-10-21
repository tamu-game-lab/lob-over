using System;
using LOBOver.XR;
using UnityEngine;

namespace LOBOver.World
{
    public sealed class InspectTarget : MonoBehaviour
    {
        public string target;
        public static event Action<InspectTarget, Vector3> Selected;
        private void Start() => Bind();
        private void Bind()
        {
            foreach (var collider in GetComponentsInChildren<Collider>(true))
            {
                if (collider.GetComponentInParent<InspectTarget>() != this || collider.GetComponent<InteractionSurface>() != null) continue;
                InteractionSurface.ForCollider(collider).Selected += position => Selected?.Invoke(this, position);
            }
        }
        public static void Attach(GameObject obj, string value)
        {
            var marker = obj.GetComponent<InspectTarget>() ?? obj.AddComponent<InspectTarget>(); marker.target = value;
            if (Application.isPlaying) marker.Bind();
        }
    }
}
