using UnityEngine;

namespace LOBOver.World
{
    public sealed class SiteSky : MonoBehaviour
    {
        public ParticleSystem rain;
        public Transform cloudBank;
        public Transform observer;
        public Transform dome;
        public LOBOver.XR.XRRig rig;
        public string State { get; private set; }
        public bool Raining => rain.gameObject.activeSelf;
        private WorldAssets assets;
        private AudioSource bed;
        private bool originalFog;
        private Color originalFogColour;
        private float originalFogDensity;
        public void Configure(WorldAssets source)
        {
            assets = source;
            if (bed == null) { bed = WorldDrawing.Sound(gameObject, true); bed.spatialBlend = 0; }
            originalFog = RenderSettings.fog; originalFogColour = RenderSettings.fogColor; originalFogDensity = RenderSettings.fogDensity;
            State = null; Apply("clear");
        }
        public void Apply(string state)
        {
            if (State == state) return;
            State = state; var raining = state == "rain";
            if (dome != null)
            {
                Shader.SetGlobalFloat("_SiteRain", raining ? 1 : 0);
                Shader.SetGlobalFloat("_SiteCloudBank", state == "towersSouth" || raining ? 1 : 0);
            }
            cloudBank.gameObject.SetActive(state == "towersSouth" || raining);
            rain.gameObject.SetActive(raining);
            if (raining && Application.isPlaying) rain.Play();
            RenderSettings.fog = raining || originalFog;
            RenderSettings.fogColor = raining ? new Color(0.48f, 0.5f, 0.52f) : originalFogColour;
            RenderSettings.fogDensity = raining ? 0.006f : originalFogDensity;
            WorldDrawing.Loop(bed, raining ? assets.rainfall : state == "towersSouth" ? assets.wind : null);
        }
        private void LateUpdate()
        {
            if (rig != null) observer = rig.ViewEye;
            if (observer == null) return;
            if (dome != null) dome.position = observer.position;
            rain.transform.position = observer.position + Vector3.up * 12;
            // WO-08: a cumulus billboard bank 2 km south of the site.
            cloudBank.forward = (cloudBank.position - observer.position).normalized;
        }
        private void OnDestroy()
        {
            if (assets == null) return;
            if (dome != null) { Shader.SetGlobalFloat("_SiteRain", 0); Shader.SetGlobalFloat("_SiteCloudBank", 0); }
            RenderSettings.fog = originalFog; RenderSettings.fogColor = originalFogColour; RenderSettings.fogDensity = originalFogDensity;
        }
    }
}
