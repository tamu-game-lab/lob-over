using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LOBOver.Client;
using LOBOver.World;
using LOBOver.XR;
using UnityEngine;

namespace LOBOver.Play
{
    public sealed class WarpController : MonoBehaviour
    {
        public HostConnection connection;
        public XRRig rig;
        public HostWorldPresenter presenter;
        public Transform site;
        public GameObject hub, wrist;
        public Renderer sphere;
        public Material dissolveMaterial, fallbackMaterial;
        public bool Busy { get; private set; }
        public bool InSite { get; private set; }
        public double SelectedStation { get; private set; }
        // WO-L2: one-second dissolve; fallback totals 0.3 seconds.
        public const float DissolveSeconds = 1, FadeSeconds = 0.3f, SphereDiameter = 4;
        private Material effect;
        private bool dissolve;
        private readonly Dictionary<Animator, float> animatorSpeeds = new Dictionary<Animator, float>();
        private readonly List<ParticleSystem> particles = new List<ParticleSystem>();
        private readonly List<CrewRig> crews = new List<CrewRig>();
        private void Start()
        {
            rig.Resolve();
            site.gameObject.SetActive(false); wrist.SetActive(false); hub.SetActive(true);
            SetPassthrough(true);
            dissolve = dissolveMaterial != null && dissolveMaterial.shader != null && dissolveMaterial.shader.isSupported;
            var material = dissolve ? dissolveMaterial : fallbackMaterial;
            if (material != null) { effect = new Material(material); sphere.sharedMaterial = effect; }
            sphere.gameObject.SetActive(false);
        }
        public bool Visit(string crewId)
        {
            if (Busy || InSite || !connection.Connected || connection.Mirror || !rig.TryHeadPose(out var pose)) return false;
            var crew = connection.View?.crews.FirstOrDefault(c => c.id == crewId);
            if (crew == null) return false;
            SelectedStation = crew.stationFt;
            Busy = true;
            presenter.Present();
            var forward = Vector3.ProjectOnPlane(pose.rotation * Vector3.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.1f) forward = Vector3.forward;
            // Road is +Z of the trench. Align -Z with gaze and put the road at the tracked feet.
            site.rotation = Quaternion.LookRotation(-forward);
            var feet = new Vector3(pose.position.x, 0, pose.position.z);
            var road = new Vector3((float)SelectedStation * WorldUnits.MetresPerFoot, 0, WorldUnits.RoadZ);
            site.position = feet - site.rotation * road;
            StartCoroutine(Transition(true));
            _ = SendArrival(SelectedStation);
            return true;
        }
        private async System.Threading.Tasks.Task SendArrival(double station)
        {
            // Transport order is intentional; dismount waits for the host's moving flag to clear.
            // PROTOCOL.md retains the mounted travel precondition, even though Play has no truck presentation.
            if (connection.View?.truck.dismounted == true && !await connection.SendCommand("mount")) return;
            if (!await connection.SendCommand("moveTo", station)) return;
            arrivalPending = true;
        }
        private bool arrivalPending;
        private void Update()
        {
            if (!arrivalPending) return;
            if (!connection.Connected) { arrivalPending = false; return; }
            var truck = connection.View?.truck;
            if (truck != null && !truck.moving && System.Math.Abs(truck.stationFt - SelectedStation) < 0.1)
            { arrivalPending = false; _ = connection.SendCommand("dismount"); }
        }
        public bool Back()
        {
            if (Busy || !InSite) return false;
            Busy = true; StartCoroutine(Transition(false)); return true;
        }
        private IEnumerator Transition(bool entering)
        {
            presenter.Frozen = true;
            if (dissolve && effect != null)
            {
                effect.SetFloat("_Progress", entering ? 0 : 1);
                sphere.gameObject.SetActive(true); FollowHead();
                SetPassthrough(true);
                site.gameObject.SetActive(true);
                FreezeWorld(entering);
                var elapsed = 0f;
                while (elapsed < DissolveSeconds)
                {
                    effect.SetFloat("_Progress", entering ? elapsed / DissolveSeconds : 1 - elapsed / DissolveSeconds);
                    yield return null; elapsed += Time.unscaledDeltaTime;
                }
            }
            else
            {
                // Built-in serialized fallback reference survives shader stripping.
                if (effect == null) { Busy = false; presenter.Frozen = false; yield break; }
                sphere.gameObject.SetActive(true); FollowHead();
                FreezeWorld(entering);
                yield return Fade(0, 1);
                site.gameObject.SetActive(entering); SetPassthrough(!entering);
                yield return Fade(1, 0);
            }
            InSite = entering;
            SetPassthrough(!entering);
            site.gameObject.SetActive(entering); hub.SetActive(!entering); wrist.SetActive(entering);
            sphere.gameObject.SetActive(false);
            ThawWorld(); presenter.Frozen = false; presenter.Present(); Busy = false;
        }
        private IEnumerator Fade(float from, float to)
        {
            var elapsed = 0f;
            while (elapsed < FadeSeconds / 2)
            {
                effect.SetFloat("_Opacity", Mathf.Lerp(from, to, elapsed / (FadeSeconds / 2)));
                yield return null; elapsed += Time.unscaledDeltaTime;
            }
            effect.SetFloat("_Opacity", to);
        }
        private void LateUpdate() { if (Busy) FollowHead(); }
        private void FollowHead()
        {
            if (rig.ViewEye == null) return;
            sphere.transform.position = rig.ViewEye.position;
            sphere.transform.rotation = Quaternion.identity;
            if (dissolve && effect != null)
            {
                effect.SetFloat("_Bottom", rig.ViewEye.position.y - SphereDiameter / 2);
                effect.SetFloat("_Span", SphereDiameter);
            }
        }
        private void FreezeWorld(bool snap)
        {
            foreach (var crew in site.GetComponentsInChildren<CrewRig>(true))
                if (crew.enabled) { if (snap) crew.SnapPose(); crews.Add(crew); crew.enabled = false; }
            foreach (var animator in site.GetComponentsInChildren<Animator>(true))
            { animatorSpeeds[animator] = animator.speed; animator.speed = 0; }
            foreach (var particle in site.GetComponentsInChildren<ParticleSystem>(true))
                if (particle.isPlaying) { particles.Add(particle); particle.Pause(); }
        }
        private void ThawWorld()
        {
            foreach (var crew in crews) if (crew != null) crew.enabled = true;
            foreach (var pair in animatorSpeeds) if (pair.Key != null) pair.Key.speed = pair.Value;
            foreach (var particle in particles) if (particle != null && particle.gameObject.activeInHierarchy) particle.Play();
            crews.Clear(); animatorSpeeds.Clear(); particles.Clear();
        }
        private void SetPassthrough(bool value)
        {
            if (rig.Passthrough == null) return;
            rig.Passthrough.enabled = value; rig.Passthrough.hidden = !value;
        }
        private void OnDestroy() { if (effect != null) Destroy(effect); }
    }
}
