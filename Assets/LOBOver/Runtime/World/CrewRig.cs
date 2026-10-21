using System;
using LOBOver.Sim;
using TMPro;
using UnityEngine;

namespace LOBOver.World
{
    public sealed class CrewRig : MonoBehaviour
    {
        public string Id { get; private set; }
        public string Status { get; private set; }
        public double StationFt { get; private set; }
        public int MoodLevel { get; private set; }
        public string MachinePlate { get; private set; }
        public Transform Foreman => workers[0];
        public Transform Partner => partner;
        public string ForemanClip => clips[0];
        public bool WorkingParticles => particles != null && particles.isPlaying;
        public GameObject Machine => machine;
        private WorldAssets assets;
        private GameObject machine, effects;
        private Animator[] machineAnimators;
        private readonly Transform[] workers = new Transform[2]; // WO-08: two workers per machine.
        private readonly Animator[] animators = new Animator[2];
        private readonly string[] clips = new string[2];
        private TextMeshPro plate;
        private ParticleSystem particles;
        private AudioSource engine, argument;
        private Transform partner;
        private Vector3 target, movementVelocity;
        private float statusTime;
        private bool positioned;
        private readonly Vector3[] workerTargets = new Vector3[2], workerVelocities = new Vector3[2];
        // Presentation cadence only; the mood level itself comes exclusively from SiteView.
        private const float PointPeriodSeconds = 10; // WO-08 section 3.
        private const float WorkingAlternateSeconds = 4;
        private static readonly string[] MoodClips = { "Idle", "IdleAlternate", "Drink", "EquipmentCheck" };
        private static readonly float[] MoodSpeeds = { 1, 0.9f, 0.8f, 0.7f };
        private void OnEnable()
        {
            if (engine != null && engine.clip != null) engine.Play();
            if (argument != null && argument.clip != null) argument.Play();
            if (Status == "working" && effects != null)
                foreach (var effect in effects.GetComponentsInChildren<ParticleSystem>()) effect.Play();
        }

        public void Initialize(string id, WorldAssets source)
        {
            Id = id; assets = source; name = "Crew_" + id;
            for (var i = 0; i < workers.Length; i++)
            {
                workers[i] = Instantiate(assets.worker, transform).transform; workers[i].name = i == 0 ? "Foreman" : "Worker";
                animators[i] = workers[i].GetComponentInChildren<Animator>();
                if (animators[i] != null) { animators[i].applyRootMotion = false; animators[i].cullingMode = AnimatorCullingMode.AlwaysAnimate; }
                InspectTarget.Attach(workers[i].gameObject, "crew");
            }
            effects = Instantiate(assets.particles, transform); effects.name = "WorkParticles";
            particles = effects.GetComponentInChildren<ParticleSystem>();
            foreach (var effect in effects.GetComponentsInChildren<ParticleSystem>()) effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            plate = WorldDrawing.Text("MachinePlate", transform, new Vector3(-1, 2.5f, 1), new Vector2(4, 0.5f), 0.25f, assets.font);
            plate.transform.localRotation = Quaternion.Euler(0, 180, 0);
            engine = WorldDrawing.Sound(gameObject, true); argument = WorldDrawing.Sound(gameObject, true, 35);
        }
        public void Apply(CrewView view)
        {
            var machineChanged = machine == null || !string.Equals(MachinePlate, view.machinePlate, StringComparison.OrdinalIgnoreCase);
            if (machineChanged)
            {
                var item = assets.Machine(Id, view.machinePlate);
                if (item == null) throw new InvalidOperationException("No imported machine for " + Id + "/" + view.machinePlate);
                WorldDrawing.Remove(machine); machine = Instantiate(item.prefab, transform); machine.name = "Machine";
                InspectTarget.Attach(machine, "machine"); machineAnimators = machine.GetComponentsInChildren<Animator>();
                foreach (var animator in machineAnimators) animator.applyRootMotion = false;
                MachinePlate = view.machinePlate; plate.text = item.label;
            }
            var next = new Vector3((float)view.stationFt * WorldUnits.MetresPerFoot, 0, 0);
            if (view.status == "demobilized") next += new Vector3(-WorldUnits.DemobBackMetres, 0, WorldUnits.RoadZ);
            if (!positioned || Math.Abs(view.stationFt - StationFt) > WorldUnits.StationFeet)
            { transform.localPosition = next; movementVelocity = Vector3.zero; positioned = true; }
            target = next;
            StationFt = view.stationFt; MoodLevel = Mathf.Clamp((int)view.moodLevel, 0, 3);
            if (Status != view.status || machineChanged)
            {
                Status = view.status; statusTime = 0; Debug.Log(name + " " + Status, this);
                var working = Status == "working";
                foreach (var animator in machineAnimators)
                {
                    animator.speed = working ? 1 : 0;
                    var cycle = Id == "exc" ? "Dig" : "Cycle";
                    if (working && animator.runtimeAnimatorController != null && animator.HasState(0, Animator.StringToHash(cycle))) animator.Play(cycle, 0, 0);
                }
                foreach (var effect in effects.GetComponentsInChildren<ParticleSystem>())
                    if (working && Application.isPlaying && effect.gameObject.activeInHierarchy) effect.Play(); else effect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                WorldDrawing.Loop(engine, working ? (Id == "exc" ? assets.excavation : Id == "back" ? assets.dozer : assets.idle) : Status == "demobilized" ? null : assets.idle);
                WorldDrawing.Loop(argument, Status == "arguing" ? assets.conflict : null);
            }
            ApplyAnimations();
        }
        public void FaceArgument(Transform other)
        {
            partner = other;
            SetWorkerTargets();
        }
        private void SetWorkerTargets()
        {
            for (var i = 0; i < workers.Length; i++)
            {
                // WO-08: workers stand 2 metres behind the working edge on the road side.
                var goal = transform.parent.TransformPoint(target + new Vector3(-2, 0, 2 + i));
                if (i == 0 && Status == "arguing" && partner != null)
                {
                    var other = partner.GetComponent<CrewRig>();
                    var midpoint = (target + other.target) / 2;
                    var side = string.CompareOrdinal(Id, other.Id) < 0 ? -1 : 1;
                    goal = transform.parent.TransformPoint(midpoint + new Vector3(side * 0.65f, 0, 3));
                }
                workerTargets[i] = goal;
            }
        }
        private void Update()
        {
            if (!positioned) return;
            statusTime += Time.deltaTime;
            // A continuous target must not restart a tween on every frame. Four damping periods
            // settle a state change within the WO's half-second presentation interval.
            transform.localPosition = Vector3.SmoothDamp(transform.localPosition, target, ref movementVelocity, WorldUnits.PoseSeconds / 4);
            SetWorkerTargets();
            for (var i = 0; i < workers.Length; i++)
            {
                workers[i].position = Vector3.SmoothDamp(workers[i].position, workerTargets[i], ref workerVelocities[i], WorldUnits.PoseSeconds / 4);
                var facing = Status == "held" ? transform.parent.forward : transform.parent.right;
                if (i == 0 && Status == "arguing" && partner != null)
                    facing = partner.GetComponent<CrewRig>().Foreman.position - workers[i].position;
                facing.y = 0;
                if (facing.sqrMagnitude > 0.001f) workers[i].rotation = Quaternion.LookRotation(facing);
            }
            ApplyAnimations();
        }
        public void SnapPose()
        {
            if (!positioned) return;
            transform.localPosition = target; movementVelocity = Vector3.zero;
            SetWorkerTargets();
            for (var i = 0; i < workers.Length; i++)
            { workers[i].position = workerTargets[i]; workerVelocities[i] = Vector3.zero; }
        }
        private bool ClipFinished(int index) => animators[index] == null || !animators[index].isActiveAndEnabled
            || animators[index].GetCurrentAnimatorStateInfo(0).normalizedTime >= 1;
        private void ApplyAnimations()
        {
            for (var i = 0; i < workers.Length; i++)
            {
                var clip = MoodClips[MoodLevel];
                if (Status == "working") clip = ((int)(statusTime / WorkingAlternateSeconds) + i) % 2 == 0 ? "Walk" : "Inspect";
                else if (Status == "held" && MoodLevel == 0) clip = "IdleAlternate";
                else if (Status == "stopped" && (statusTime == 0 || clips[i] == "SignalHalt" && !ClipFinished(i))) clip = "SignalHalt";
                else if (Status == "arguing") clip = i == 0 && string.CompareOrdinal(Id, partner == null ? "" : partner.GetComponent<CrewRig>().Id) < 0 ? "ConflictTalk" : "TalkAlternate";
                else if (Status == "demobilized") clip = "Shelter";
                else if (MoodLevel == 3 && statusTime >= PointPeriodSeconds && statusTime % PointPeriodSeconds < 1) clip = "SignalPoint";
                if (clips[i] != clip)
                {
                    clips[i] = clip;
                    if (animators[i] != null && animators[i].runtimeAnimatorController != null && animators[i].HasState(0, Animator.StringToHash(clip)))
                        animators[i].Play(clip, 0, 0);
                }
                if (animators[i] != null) animators[i].speed = clip == MoodClips[MoodLevel] ? MoodSpeeds[MoodLevel] : 1;
            }
        }
    }
}
