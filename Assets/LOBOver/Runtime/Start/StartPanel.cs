using System.Collections;
using LOBOver.Runtime;
using LOBOver.MR;
using LOBOver.VR;
using LOBOver.World;
using TMPro;
using UnityEngine;

namespace LOBOver.Start
{
    public enum ShowcaseMode { VRSite, MRDiorama, Desktop }

    // Runs before Director.OnEnable, so opening the scene never starts the simulation behind the menu.
    [DefaultExecutionOrder(-500)]
    public sealed class StartPanel : MonoBehaviour
    {
        public Director director;
        public WorldAssets assets;
        public AudioClip clickClip;
        public OVRCameraRig rig;
        public OVRManager manager;
        public OVRPassthroughLayer passthrough;
        public ControllerPointer pointer;
        public GameObject fullScaleRoot, vrRoot, mrRoot, desktopRoot, fullScalePlan;
        public Transform panel;
        public Transform listener, desktopEye;
        public GameObject worldTruck;
        public DioramaSeat diorama;
        public ShowcaseMode SelectedMode { get; private set; }
        public string SelectedScenario { get; private set; } = "slice_texas";
        public bool Autoplan { get; private set; } = true;
        public bool Running { get; private set; }
        private TextMeshPro selection;
        private bool beginning;
        private const float PanelAhead = 1.5f; // WO-S4 item 1.
        private static readonly string[] Scenarios = { "slice_texas", "stage1_texas", "stage2_texas" };
        private static readonly string[] ScenarioNames = { "Slice", "Stage 1", "Stage 2" };
        private void Awake()
        {
            if (director == null) return;
            director.enabled = false;
            ReadPreferences();
            SetRoots(false, false, false, false);
            if (panel != null && assets != null) BuildControls();
        }
        private IEnumerator Start()
        {
            // Allow the rig's first tracking update before placing the eye-height menu.
            yield return null;
            if (panel == null || rig == null) yield break;
            var eye = rig.centerEyeAnchor;
            var forward = Vector3.ProjectOnPlane(eye.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.1f) forward = Vector3.forward;
            panel.position = eye.position + forward * PanelAhead;
            panel.rotation = Quaternion.LookRotation(forward);
            ConfigurePerformance();
        }
        private static void ConfigurePerformance()
        {
            if (!OVRManager.isHmdPresent) return;
            // WO-S4 item 5: 72 Hz and fixed foveation level 2 (Medium).
            if (OVRManager.display != null) OVRManager.display.displayFrequency = 72;
            OVRManager.useDynamicFoveatedRendering = false;
            OVRManager.foveatedRenderingLevel = OVRManager.FoveatedRenderingLevel.Medium;
        }
        private void ReadPreferences()
        {
            SelectedMode = (ShowcaseMode)Mathf.Clamp(PlayerPrefs.GetInt("LOBOver.Mode", 0), 0, 2);
#if UNITY_ANDROID && !UNITY_EDITOR
            if (SelectedMode == ShowcaseMode.Desktop) SelectedMode = ShowcaseMode.VRSite;
#endif
            SelectedScenario = PlayerPrefs.GetString("LOBOver.Scenario", Scenarios[0]);
            if (System.Array.IndexOf(Scenarios, SelectedScenario) < 0) SelectedScenario = Scenarios[0];
        }
        public void SelectMode(ShowcaseMode mode) { SelectedMode = mode; Refresh(); }
        public void SelectScenario(string scenario)
        {
            if (System.Array.IndexOf(Scenarios, scenario) < 0) return;
            SelectedScenario = scenario; Refresh();
        }
        private void BuildControls()
        {
            selection = WorldDrawing.Text("Selection", panel, new Vector3(0, 0.31f, -0.025f), new Vector2(1.1f, 0.16f), 0.035f, assets.font);
            var names = new[] { "VR Site", "MR Diorama", "Desktop" };
            for (var i = 0; i < names.Length; i++)
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                if (i == 2) continue;
#endif
                var mode = (ShowcaseMode)i;
                RayButton.Create(panel, names[i], new Vector3((i - 1) * 0.37f, 0.11f, -0.025f), new Vector2(0.34f, 0.14f), assets, clickClip, () => SelectMode(mode));
            }
            for (var i = 0; i < Scenarios.Length; i++)
            {
                var scenario = Scenarios[i];
                RayButton.Create(panel, ScenarioNames[i], new Vector3((i - 1) * 0.37f, -0.08f, -0.025f), new Vector2(0.34f, 0.12f), assets, clickClip, () => SelectScenario(scenario));
            }
            RayButton.Create(panel, "Autoplan", new Vector3(-0.25f, -0.28f, -0.025f), new Vector2(0.44f, 0.13f), assets, clickClip, () => { Autoplan = !Autoplan; Refresh(); });
            RayButton.Create(panel, "Start", new Vector3(0.25f, -0.28f, -0.025f), new Vector2(0.44f, 0.13f), assets, clickClip, Begin);
            Refresh();
        }
        private void Refresh()
        {
            PlayerPrefs.SetInt("LOBOver.Mode", (int)SelectedMode); PlayerPrefs.SetString("LOBOver.Scenario", SelectedScenario); PlayerPrefs.Save();
            if (selection != null) selection.text = "LOB, Over\n" + new[] { "VR Site", "MR Diorama", "Desktop" }[(int)SelectedMode] + " / " + ScenarioNames[System.Array.IndexOf(Scenarios, SelectedScenario)] + " / Autoplan " + (Autoplan ? "on" : "off");
        }
        public void Begin()
        {
            if (beginning || Running) return;
            beginning = true; StartCoroutine(BeginAfterRecenter());
        }
        private IEnumerator BeginAfterRecenter()
        {
            if (OVRManager.isHmdPresent && OVRManager.display != null) OVRManager.display.RecenterPose();
            ConfigurePerformance();
            yield return null;
            ActivateMode(SelectedMode);
            director.AutoplanEnabled = Autoplan; director.scenarioId = SelectedScenario;
            director.enabled = true;
            // OnEnable performs the first load. Reusing the panel in tests may find a ready Director.
            if (director.Ready) director.Load(SelectedScenario);
            beginning = false;
        }
        // Public seam for PlayMode tests: only root routing; it does not require XR or start a job.
        public void ActivateMode(ShowcaseMode mode)
        {
            SelectedMode = mode; Running = true;
            var vr = mode == ShowcaseMode.VRSite; var mr = mode == ShowcaseMode.MRDiorama;
            // Move the shared rig out before disabling a previous cab.
            if (rig != null) rig.transform.SetParent(null, true);
            if (listener != null) listener.SetParent(mode == ShowcaseMode.Desktop ? desktopEye : rig.centerEyeAnchor, false);
            SetRoots(vr || mode == ShowcaseMode.Desktop, vr, mr, mode == ShowcaseMode.Desktop);
            if (fullScalePlan != null) fullScalePlan.SetActive(mode == ShowcaseMode.Desktop);
            if (worldTruck != null) worldTruck.SetActive(mode != ShowcaseMode.VRSite);
            if (panel != null) panel.gameObject.SetActive(false);
            if (rig != null) rig.gameObject.SetActive(vr || mr);
            if (pointer != null) pointer.gameObject.SetActive(vr || mr);
            if (manager != null) manager.isInsightPassthroughEnabled = mr;
            if (passthrough != null) passthrough.enabled = mr;
            if (rig != null)
                foreach (var camera in rig.GetComponentsInChildren<Camera>(true))
                {
                    // Same background settings as Meta's Passthrough Building Block.
                    camera.clearFlags = mr ? CameraClearFlags.SolidColor : CameraClearFlags.Skybox;
                    camera.backgroundColor = mr ? Color.clear : Color.black;
                }
            if (mr && diorama != null) diorama.Place();
        }
        private void SetRoots(bool full, bool vr, bool mr, bool desktop)
        {
            if (fullScaleRoot != null) fullScaleRoot.SetActive(full);
            if (vrRoot != null) vrRoot.SetActive(vr);
            if (mrRoot != null) mrRoot.SetActive(mr);
            if (desktopRoot != null) desktopRoot.SetActive(desktop);
        }
        private void Update()
        {
            if (director != null && director.LoadError != null && selection != null)
            { panel.gameObject.SetActive(true); selection.text = director.LoadError; }
        }
    }
}
