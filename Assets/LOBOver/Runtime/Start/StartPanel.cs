using System.Collections;
using LOBOver.Runtime;
using LOBOver.MR;
using LOBOver.VR;
using LOBOver.World;
using TMPro;
using UnityEngine;
using LOBOver.XR;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections.Generic;

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
        public XRRig xr;
        public DesktopPreview preview;
        public GameObject fullScaleRoot, vrRoot, mrRoot, desktopRoot, fullScalePlan;
        public Transform panel;
        public GameObject worldTruck;
        public DioramaSeat diorama;
        public ShowcaseMode SelectedMode { get; private set; }
        public string SelectedScenario { get; private set; } = "slice_texas";
        public bool Autoplan { get; private set; } = true;
        public bool Running { get; private set; }
        private TextMeshProUGUI selection;
        public bool Placed { get; private set; }
#if UNITY_EDITOR
        private Vector3 previewPosition;
        private Quaternion previewRotation;
#endif
        private bool beginning;
        private const float PanelAhead = 0.6f; // WO-S6 item 7.
        private const float ChestBelowEye = 0.3f; // Presentation offset for chest-height placement (WO-S6).
        private static readonly string[] Scenarios = { "slice_texas", "stage1_texas", "stage2_texas" };
        private static readonly string[] ScenarioNames = { "Slice", "Stage 1", "Stage 2" };
        private void Awake()
        {
            if (director == null) return;
            director.enabled = false;
            ReadPreferences();
            SetRoots(false, false, false, false);
            if (panel != null && assets != null) { BuildControls(); panel.gameObject.SetActive(false); }
#if UNITY_EDITOR
            if (preview != null)
            {
                previewPosition = preview.previewCamera.transform.position; previewRotation = preview.previewCamera.transform.rotation;
            }
            if (preview != null && !OVRManager.isHmdPresent)
            {
                preview.enabled = false; desktopRoot.SetActive(true);
                if (xr != null && xr.TryHeadPose(out var pose)) preview.previewCamera.transform.SetPositionAndRotation(pose.position, pose.rotation);
            }
#endif
        }
        private IEnumerator Start()
        {
            yield return null;
            if (panel == null || xr == null) yield break;
            Pose pose;
            while (!xr.TryHeadPose(out pose)) yield return null;
            PlaceOnce(pose);
        }
        public void PlaceOnce(Pose pose)
        {
            if (Placed || panel == null) return;
            var forward = Vector3.ProjectOnPlane(pose.rotation * Vector3.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.1f) forward = Vector3.forward;
            panel.SetPositionAndRotation(pose.position + forward * PanelAhead - Vector3.up * ChestBelowEye, Quaternion.LookRotation(forward));
            panel.gameObject.SetActive(true); Placed = true;
#if UNITY_EDITOR
            if (preview != null && !OVRManager.isHmdPresent)
                preview.previewCamera.transform.SetPositionAndRotation(pose.position, Quaternion.LookRotation(panel.position - pose.position));
#endif
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
            selection = Label("Selection", new Vector2(0, 310), new Vector2(1100, 160), 35);
            var names = new[] { "VR Site", "MR Diorama", "Desktop" };
            for (var i = 0; i < names.Length; i++)
            {
#if !UNITY_EDITOR
                if (i == 2) continue;
#endif
                var mode = (ShowcaseMode)i;
                Button(names[i], new Vector2((i - 1) * 370, 110), new Vector2(340, 140), () => SelectMode(mode));
            }
            for (var i = 0; i < Scenarios.Length; i++)
            {
                var scenario = Scenarios[i];
                Button(ScenarioNames[i], new Vector2((i - 1) * 370, -80), new Vector2(340, 120), () => SelectScenario(scenario));
            }
            Button("Autoplan", new Vector2(-250, -280), new Vector2(440, 130), () => { Autoplan = !Autoplan; Refresh(); });
            Button("Start", new Vector2(250, -280), new Vector2(440, 130), Begin);
            Refresh();
        }
        private TextMeshProUGUI Label(string name, Vector2 position, Vector2 size, float fontSize)
        {
            var obj = new GameObject(name, typeof(RectTransform)); obj.transform.SetParent(panel, false);
            var label = obj.AddComponent<TextMeshProUGUI>(); label.font = assets.font; label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center; label.richText = false; label.raycastTarget = false;
            label.rectTransform.anchoredPosition = position; label.rectTransform.sizeDelta = size; return label;
        }
        private void Button(string title, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            var obj = new GameObject(title, typeof(RectTransform)); obj.transform.SetParent(panel, false);
            var rect = (RectTransform)obj.transform; rect.anchoredPosition = position; rect.sizeDelta = size;
            var image = obj.AddComponent<Image>(); image.color = new Color(0.85f, 0.43f, 0.15f);
            var button = obj.AddComponent<Button>(); button.targetGraphic = image; button.transition = Selectable.Transition.None;
            var sound = obj.AddComponent<AudioSource>(); sound.playOnAwake = false; sound.spatialBlend = 1; sound.clip = clickClip;
            button.onClick.AddListener(() => { if (sound.clip != null) sound.Play(); }); button.onClick.AddListener(action);
            var label = Label(title + "Label", Vector2.zero, size * 0.9f, 35); label.text = title; label.transform.SetParent(rect, false);
        }
        private void Refresh()
        {
            PlayerPrefs.SetInt("LOBOver.Mode", (int)SelectedMode); PlayerPrefs.SetString("LOBOver.Scenario", SelectedScenario); PlayerPrefs.Save();
            if (selection != null) selection.text = "LOB, Over\n" + new[] { "VR Site", "MR Diorama", "Desktop" }[(int)SelectedMode] + " / " + ScenarioNames[System.Array.IndexOf(Scenarios, SelectedScenario)] + " / Autoplan " + (Autoplan ? "on" : "off");
        }
        public void Begin()
        {
            if (beginning || Running) return;
            beginning = true; StartCoroutine(BeginMode());
        }
        private IEnumerator BeginMode()
        {
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
            SetRoots(vr || mode == ShowcaseMode.Desktop, vr, mr, mode == ShowcaseMode.Desktop);
            if (fullScalePlan != null) fullScalePlan.SetActive(mode == ShowcaseMode.Desktop);
            if (worldTruck != null) worldTruck.SetActive(mode != ShowcaseMode.VRSite);
            if (panel != null) panel.gameObject.SetActive(false);
#if UNITY_EDITOR
            if (preview != null)
            {
                preview.previewCamera.transform.SetPositionAndRotation(previewPosition, previewRotation);
                preview.enabled = mode == ShowcaseMode.Desktop;
            }
#endif
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
#if UNITY_EDITOR
            // Editor mouse uses the same uGUI Button events; device presses come solely from ISDK.
            if (!Running && Placed && preview != null && !OVRManager.isHmdPresent && Mouse.current != null
                && Mouse.current.leftButton.wasReleasedThisFrame && EventSystem.current != null)
            {
                var data = new PointerEventData(EventSystem.current) { position = Mouse.current.position.ReadValue() };
                var hits = new List<RaycastResult>();
                panel.GetComponent<Canvas>().worldCamera = preview.previewCamera;
                panel.GetComponent<GraphicRaycaster>().Raycast(data, hits);
                if (hits.Count > 0) ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerClickHandler);
            }
#endif
            if (director != null && director.LoadError != null && selection != null)
            { panel.gameObject.SetActive(true); selection.text = director.LoadError; }
        }
    }
}
