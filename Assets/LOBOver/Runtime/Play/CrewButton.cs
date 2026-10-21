using LOBOver.XR;
using Oculus.Interaction;
using UnityEngine;

namespace LOBOver.Play
{
    public sealed class CrewButton : MonoBehaviour
    {
        public WarpController warp;
        public string crew;
        public AudioSource click;
        public bool Available { get; private set; }
        private InteractionSurface surface;
        private void OnEnable()
        {
            surface = GetComponent<InteractionSurface>();
            if (surface != null) surface.Selected += Press;
        }
        private void OnDisable() { if (surface != null) surface.Selected -= Press; }
        private void Update()
        {
            Available = warp != null && !warp.Busy && (string.IsNullOrEmpty(crew)
                ? warp.InSite : warp.connection.Connected && !warp.connection.Mirror && warp.connection.View != null);
            var ray = GetComponent<RayInteractable>(); var poke = GetComponent<PokeInteractable>();
            if (ray != null) ray.enabled = Available;
            if (poke != null) poke.enabled = Available;
        }
        private void Press(Vector3 ignored)
        {
            if (!Available) return;
            var accepted = string.IsNullOrEmpty(crew) ? warp.Back() : warp.Visit(crew);
            if (accepted && click != null && click.clip != null) click.Play();
        }
    }
}
