using LOBOver.XR;
using Oculus.Interaction;
using UnityEngine;
#if UNITY_EDITOR
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine.InputSystem;
#endif

namespace LOBOver.Play
{
    // The root and head camera are EditorOnly. Mouse clicks exercise the same ISDK event as a ray selection.
    public sealed class PlayPreview : MonoBehaviour
    {
        public Camera view;
        public Camera survey;
        public XRRig rig;
        public CrewButton back;
        private void Start()
        {
#if UNITY_EDITOR
            if (OVRManager.isHmdPresent) { view.enabled = false; gameObject.SetActive(false); }
            else AlignHeadCamera();
#else
            gameObject.SetActive(false);
#endif
        }
        private void Update()
        {
#if UNITY_EDITOR
            var mouse = Mouse.current; var keyboard = Keyboard.current;
            if (mouse == null) return;
            if (mouse.leftButton.wasPressedThisFrame && Physics.Raycast(view.ScreenPointToRay(mouse.position.ReadValue()), out var hit))
            {
                var button = hit.collider.GetComponentInParent<CrewButton>();
                if (button != null) Select(button);
            }
            if (keyboard != null && keyboard.bKey.wasPressedThisFrame && back.gameObject.activeInHierarchy) Select(back);
#endif
        }
        private static void Select(CrewButton button)
        {
            var ray = button.GetComponent<RayInteractable>();
            if (!button.Available || ray == null) return;
            var pose = new Pose(button.transform.position, button.transform.rotation);
            ray.PublishPointerEvent(new PointerEvent(-1, PointerEventType.Hover, pose));
            ray.PublishPointerEvent(new PointerEvent(-1, PointerEventType.Select, pose));
            ray.PublishPointerEvent(new PointerEvent(-1, PointerEventType.Unselect, pose));
            ray.PublishPointerEvent(new PointerEvent(-1, PointerEventType.Unhover, pose));
        }
#if UNITY_EDITOR
        private void LateUpdate() => AlignHeadCamera();
        public void AlignHeadCamera()
        {
            if (rig.CameraRig == null) rig.Resolve(false);
            var eye = rig.CentreEye == null ? null : rig.CentreEye.GetComponent<Camera>();
            if (eye == null) throw new InvalidOperationException("Head preview requires the rig's centre eye camera.");
            if (view.transform.parent != eye.transform) view.transform.SetParent(eye.transform, false);
            view.transform.localPosition = Vector3.zero; view.transform.localRotation = Quaternion.identity;
            view.transform.localScale = Vector3.one;
            view.fieldOfView = eye.fieldOfView; view.nearClipPlane = eye.nearClipPlane; view.farClipPlane = eye.farClipPlane;
            view.cullingMask = eye.cullingMask;
            view.stereoTargetEye = StereoTargetEyeMask.None;
        }
        // WO-L5 item 6 / WO-L4 preview resolution.
        public const int FrameSize = 1440;
        private static readonly float[] WarpSteps = { 0.2f, 0.45f, 0.7f, 0.9f, 1f };
        private static readonly string OutputFolder = "Monitor/out/unity";
        [MenuItem("LOB Over/Debug/Warp Frames")]
        public static void WarpFrames()
        {
            var preview = FindFirstObjectByType<PlayPreview>();
            if (!Application.isPlaying || preview == null)
                throw new InvalidOperationException("Warp Frames requires the Play scene in Play Mode.");
            var warp = preview.back.warp;
            if (warp.Busy) throw new InvalidOperationException("Wait for the current warp to finish before capturing Warp Frames.");
            if (!warp.InSite && !warp.Visit("pipe"))
                throw new InvalidOperationException("Warp Frames requires a connected Node host and Marcus's crew.");
            preview.StartCoroutine(preview.CaptureWhenReady(warp));
        }
        private IEnumerator CaptureWhenReady(WarpController warp)
        {
            while (warp.Busy) yield return null;
            if (!warp.InSite) yield break;
            WriteWarpFrames(warp);
        }
        public void WriteWarpFrames(WarpController warp)
        {
            Directory.CreateDirectory(OutputFolder);
            var roomPath = Path.Combine(OutputFolder, "room.png");
            EnsureRoom(roomPath);
            var room = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var composite = new Texture2D(FrameSize, FrameSize, TextureFormat.RGBA32, false);
            var progress = warp.Progress; var frozen = warp.presenter.Frozen;
            var hubVisible = warp.hub.activeSelf; var wristVisible = warp.wrist.activeSelf;
            try
            {
                if (!room.LoadImage(File.ReadAllBytes(roomPath))) throw new IOException("Cannot decode " + roomPath);
                warp.presenter.Frozen = true;
                // Capture the site eye buffer alone; the panel would occlude its lower centre.
                warp.hub.SetActive(false); warp.wrist.SetActive(false);
                foreach (var step in WarpSteps)
                {
                    warp.PreviewProgress(step);
                    var frame = CaptureFrame();
                    try
                    {
                        var pixels = frame.GetPixels();
                        for (var y = 0; y < FrameSize; y++) for (var x = 0; x < FrameSize; x++)
                        {
                            var index = y * FrameSize + x;
                            var wall = room.GetPixelBilinear((x + 0.5f) / FrameSize, (y + 0.5f) / FrameSize);
                            pixels[index] = Composite(pixels[index], wall, QualitySettings.activeColorSpace == ColorSpace.Linear);
                        }
                        composite.SetPixels(pixels); composite.Apply();
                        var label = step.ToString("0.0#", CultureInfo.InvariantCulture);
                        File.WriteAllBytes(Path.Combine(OutputFolder, "warp-" + label + ".png"), composite.EncodeToPNG());
                    }
                    finally { DestroyImmediate(frame); }
                }
            }
            finally
            {
                warp.PreviewProgress(progress); warp.presenter.Frozen = frozen;
                warp.hub.SetActive(hubVisible); warp.wrist.SetActive(wristVisible);
                DestroyImmediate(room); DestroyImmediate(composite);
            }
            Debug.Log("LOBOver Warp Frames: wrote 5 alpha composites to " + OutputFolder);
        }
        public Texture2D CaptureFrame()
        {
            AlignHeadCamera();
            var target = RenderTexture.GetTemporary(FrameSize, FrameSize, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var previousTarget = view.targetTexture; var previousActive = RenderTexture.active;
            var previousHdr = view.allowHDR; var previousClear = view.clearFlags; var previousBackground = view.backgroundColor;
            var frame = new Texture2D(FrameSize, FrameSize, TextureFormat.RGBA32, false, true);
            try
            {
                view.allowHDR = false; view.clearFlags = CameraClearFlags.SolidColor; view.backgroundColor = Color.clear;
                view.targetTexture = target; view.Render(); RenderTexture.active = target;
                frame.ReadPixels(new Rect(0, 0, FrameSize, FrameSize), 0, 0); frame.Apply(); return frame;
            }
            catch { DestroyImmediate(frame); throw; }
            finally
            {
                view.targetTexture = previousTarget; view.allowHDR = previousHdr;
                view.clearFlags = previousClear; view.backgroundColor = previousBackground;
                RenderTexture.active = previousActive; RenderTexture.ReleaseTemporary(target);
            }
        }
        public static Texture2D CompositeFrame(Texture2D frame, Texture2D room)
        {
            var pixels = frame.GetPixels();
            for (var y = 0; y < frame.height; y++) for (var x = 0; x < frame.width; x++)
                pixels[y * frame.width + x] = Composite(pixels[y * frame.width + x],
                    room.GetPixelBilinear((x + 0.5f) / frame.width, (y + 0.5f) / frame.height), QualitySettings.activeColorSpace == ColorSpace.Linear);
            var composite = new Texture2D(frame.width, frame.height, TextureFormat.RGBA32, false);
            composite.SetPixels(pixels); composite.Apply(); return composite;
        }
        public static Color Composite(Color premultipliedFrame, Color roomSrgb, bool linear)
        {
            // The eye's RGB is already premultiplied: multiplying it by alpha again darkens the wire.
            var result = premultipliedFrame + (linear ? roomSrgb.linear : roomSrgb) * (1 - premultipliedFrame.a);
            if (linear) result = result.gamma;
            result.a = 1; return result;
        }
        public static void EnsureRoom(string path)
        {
            if (File.Exists(path)) return;
            // WO-L5: a mid-grey wall, 20 cm grid. Stand-in wall spans four metres.
            const float WallMetres = 4, GridMetres = 0.2f;
            var spacing = Mathf.RoundToInt(FrameSize * GridMetres / WallMetres);
            var image = new Texture2D(FrameSize, FrameSize, TextureFormat.RGBA32, false);
            try
            {
                var pixels = new Color32[FrameSize * FrameSize];
                for (var y = 0; y < FrameSize; y++) for (var x = 0; x < FrameSize; x++)
                {
                    var shade = (byte)(x % spacing < 2 || y % spacing < 2 ? 100 : 128);
                    pixels[y * FrameSize + x] = new Color32(shade, shade, shade, 255);
                }
                image.SetPixels32(pixels); image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally { DestroyImmediate(image); }
        }
#endif
    }
}
