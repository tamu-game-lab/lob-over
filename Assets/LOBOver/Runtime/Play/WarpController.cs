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
        public bool Busy { get; private set; }
        public bool InSite { get; private set; }
        public float Progress { get; private set; }
        public Vector3 Origin { get; private set; }
        public double SelectedStation { get; private set; }
        public event System.Action Changed;
        // WO-L5 items 1 and 4: metres and unscaled seconds. Quadratic ease-out for the front.
        public const float InSeconds = 1.5f, OutSeconds = 1.2f, WatchdogSeconds = 3;
        public const float WaveMetres = 160, BandMetres = 6, RestRadius = 1e6f;
        private Coroutine transition;
        private bool entering;
        private double transitionStarted;
        private readonly Dictionary<Animator, float> animatorSpeeds = new Dictionary<Animator, float>();
        private readonly List<ParticleSystem> particles = new List<ParticleSystem>();
        private readonly List<ParticleSystemRenderer> particleRenderers = new List<ParticleSystemRenderer>();
        private readonly List<CrewRig> crews = new List<CrewRig>();
        private void Start()
        {
            rig.Resolve();
            site.gameObject.SetActive(false); wrist.SetActive(false); hub.SetActive(true);
            SetPassthrough(true);
            ApplyProgress(0);
        }
        public bool Visit(string crewId)
        {
            if (Busy || InSite || !connection.Connected || connection.Mirror || !rig.TryHeadPose(out var pose)) return false;
            var crew = connection.View?.crews.FirstOrDefault(c => c.id == crewId);
            if (crew == null) return false;
            SelectedStation = crew.stationFt;
            presenter.Present();
            var forward = Vector3.ProjectOnPlane(pose.rotation * Vector3.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.1f) forward = Vector3.forward;
            // Road is +Z of the trench. Align -Z with gaze and put the road at the tracked feet.
            site.rotation = Quaternion.LookRotation(-forward);
            var feet = new Vector3(pose.position.x, 0, pose.position.z);
            var road = new Vector3((float)SelectedStation * WorldUnits.MetresPerFoot, 0, WorldUnits.RoadZ);
            site.position = feet - site.rotation * road;
            BeginTransition(true);
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
            // Separate from the coroutine so StopAllCoroutines cannot strand the player.
            if (Busy && Time.realtimeSinceStartupAsDouble - transitionStarted >= WatchdogSeconds)
            {
                LogPhase("watchdog");
                if (transition != null) StopCoroutine(transition);
                FinishTransition();
            }
            if (!arrivalPending) return;
            if (!connection.Connected) { arrivalPending = false; return; }
            var truck = connection.View?.truck;
            if (truck != null && !truck.moving && System.Math.Abs(truck.stationFt - SelectedStation) < 0.1)
            { arrivalPending = false; _ = connection.SendCommand("dismount"); }
        }
        public bool Back()
        {
            // Repeated Back is harmless and never restarts the watchdog's clock.
            if (Busy && !entering) return true;
            if (!Busy && !InSite) return true;
            BeginTransition(false); return true;
        }
        private void BeginTransition(bool toSite)
        {
            if (transition != null) StopCoroutine(transition);
            var alreadyFrozen = Busy;
            entering = toSite; Busy = true;
            transitionStarted = Time.realtimeSinceStartupAsDouble;
            presenter.Frozen = true;
            SetPassthrough(true);
            ApplyProgress(Progress);
            site.gameObject.SetActive(true);
            wrist.SetActive(true);
            if (!alreadyFrozen) FreezeWorld(entering);
            LogPhase(entering ? "in start" : "out start");
            Changed?.Invoke();
            transition = StartCoroutine(Transition());
        }
        private IEnumerator Transition()
        {
            var from = Progress;
            var target = entering ? 1f : 0f;
            // Reversal keeps the same progress speed, starting exactly where Back was pressed.
            var duration = (entering ? InSeconds : OutSeconds) * Mathf.Abs(target - from);
            while (Time.realtimeSinceStartupAsDouble - transitionStarted < duration)
            {
                ApplyProgress(Mathf.Lerp(from, target, (float)(Time.realtimeSinceStartupAsDouble - transitionStarted) / duration));
                yield return null;
            }
            FinishTransition();
        }
        private void FinishTransition()
        {
            ApplyProgress(entering ? 1 : 0);
            InSite = entering;
            SetPassthrough(!entering);
            site.gameObject.SetActive(entering); hub.SetActive(!entering); wrist.SetActive(entering);
            ThawWorld(); presenter.Frozen = false; presenter.Present(); Busy = false;
            transition = null;
            LogPhase(entering ? "in done" : "out done");
            Changed?.Invoke();
        }
        public static float RadiusAt(float progress)
        {
            var p = Mathf.Clamp01(progress);
            return p >= 1 ? RestRadius : WaveMetres * (1 - (1 - p) * (1 - p));
        }
        private void ApplyProgress(float value)
        {
            Progress = Mathf.Clamp01(value);
            if (rig.TryHeadPose(out var pose)) Origin = new Vector3(pose.position.x, site.position.y, pose.position.z);
            Shader.SetGlobalFloat("_WarpProgress", Progress);
            Shader.SetGlobalVector("_WarpOrigin", Origin);
            Shader.SetGlobalFloat("_WarpRadius", RadiusAt(Progress));
            Shader.SetGlobalFloat("_WarpBand", BandMetres);
        }
        private void LogPhase(string phase) => Debug.Log("LOBOver Warp: " + phase + " progress=" + Progress.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
#if UNITY_EDITOR
        // Synchronous preview capture restores this value before the next runtime frame.
        private bool previewFrozen, previewPresenterWasFrozen;
        public void PreviewProgress(float value)
        {
            if (Busy) throw new System.InvalidOperationException("Wait for the warp before stepping preview progress.");
            if (value < 1 && !previewFrozen)
            {
                previewPresenterWasFrozen = presenter.Frozen; presenter.Frozen = true;
                FreezeWorld(false); previewFrozen = true;
            }
            else if (value >= 1 && previewFrozen)
            {
                ThawWorld(); presenter.Frozen = previewPresenterWasFrozen; previewFrozen = false;
            }
            ApplyProgress(value);
        }
#endif
        private void FreezeWorld(bool snap)
        {
            foreach (var crew in site.GetComponentsInChildren<CrewRig>(true))
                if (crew.enabled) { if (snap) crew.SnapPose(); crews.Add(crew); crew.enabled = false; }
            foreach (var animator in site.GetComponentsInChildren<Animator>(true))
            { animatorSpeeds[animator] = animator.speed; animator.speed = 0; }
            foreach (var particle in site.GetComponentsInChildren<ParticleSystem>(true))
                if (particle.isPlaying) { particles.Add(particle); particle.Pause(); }
            // Particles use URP's particle shader, so keep them absent until the material wave ends.
            foreach (var renderer in site.GetComponentsInChildren<ParticleSystemRenderer>(true))
                if (renderer.enabled) { particleRenderers.Add(renderer); renderer.enabled = false; }
        }
        private void ThawWorld()
        {
            foreach (var crew in crews) if (crew != null) crew.enabled = true;
            foreach (var pair in animatorSpeeds) if (pair.Key != null) pair.Key.speed = pair.Value;
            foreach (var particle in particles) if (particle != null && particle.gameObject.activeInHierarchy) particle.Play();
            foreach (var renderer in particleRenderers) if (renderer != null) renderer.enabled = true;
            crews.Clear(); animatorSpeeds.Clear(); particles.Clear(); particleRenderers.Clear();
        }
        private void SetPassthrough(bool value)
        {
            if (rig.Passthrough == null) return;
            rig.Passthrough.enabled = value; rig.Passthrough.hidden = !value;
        }
        private void OnDestroy()
        {
            ThawWorld();
            if (presenter != null) presenter.Frozen = false;
            Shader.SetGlobalFloat("_WarpProgress", 1);
            Shader.SetGlobalFloat("_WarpRadius", RestRadius);
        }
    }
}
