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
        public bool Available => warp != null && (string.IsNullOrEmpty(crew)
            ? warp.InSite || warp.Busy
            : !warp.Busy && !warp.InSite && warp.connection.Connected && !warp.connection.Mirror && warp.connection.View != null);
        private InteractionSurface surface;
        private Renderer face;
        private MaterialPropertyBlock tint;
        private Color normalColour;
        private void OnEnable()
        {
            surface = GetComponent<InteractionSurface>();
            if (surface != null) surface.Selected += Press;
            face = transform.Find("Face")?.GetComponent<Renderer>();
            if (face != null)
            {
                tint = new MaterialPropertyBlock();
                normalColour = face.sharedMaterial.GetColor("_BaseColor");
            }
        }
        private void OnDisable() { if (surface != null) surface.Selected -= Press; }
        private void Update()
        {
            var ray = GetComponent<RayInteractable>(); var poke = GetComponent<PokeInteractable>();
            if (ray != null) ray.enabled = Available;
            if (poke != null) poke.enabled = Available;
            if (face != null)
            {
                var grey = normalColour.grayscale;
                tint.SetColor("_BaseColor", warp != null && warp.Busy && !string.IsNullOrEmpty(crew)
                    ? new Color(grey, grey, grey, 1) : normalColour);
                face.SetPropertyBlock(tint);
            }
        }
        private void Press(Vector3 ignored)
        {
            if (!Available) return;
            var accepted = string.IsNullOrEmpty(crew) ? warp.Back() : warp.Visit(crew);
            if (accepted && click != null && click.clip != null) click.Play();
        }
    }
}
