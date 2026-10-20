using System;
using LOBOver.World;
using TMPro;
using UnityEngine;

namespace LOBOver.VR
{
    public sealed class RayButton : MonoBehaviour
    {
        public TextMeshPro label;
        public Transform face;
        public AudioSource click;
        public bool Available = true;
        public Action Pressed;
        private float depressedUntil;
        // WO-09 section 5: 5 mm depression; the short click uses hydraulics_01.
        public void Press()
        {
            depressedUntil = Time.unscaledTime + 0.12f;
            if (click != null && click.clip != null) click.Play();
            Pressed?.Invoke();
        }
        private void Update()
        {
            if (face != null) face.localPosition = new Vector3(0, 0, Time.unscaledTime < depressedUntil ? 0.005f : 0);
            if (label != null) label.color = Available ? Color.white : Color.gray;
        }
        public static RayButton Create(Transform parent, string title, Vector3 position, Vector2 size, WorldAssets assets, AudioClip sound, Action action)
        {
            var root = WorldDrawing.Group(title, parent); root.transform.localPosition = position;
            var button = root.AddComponent<RayButton>();
            button.face = WorldDrawing.Box("SafetyOrange", root.transform, Vector3.zero, new Vector3(size.x, size.y, 0.01f), assets.orange).transform;
            button.label = WorldDrawing.Text("Label", root.transform, new Vector3(0, 0, -0.006f), size * 0.9f, size.y * 0.27f, assets.font);
            button.label.alignment = TextAlignmentOptions.Center; button.label.text = title;
            button.click = WorldDrawing.Sound(root, false, 2); button.click.clip = sound;
            button.Pressed = action; return button;
        }
    }
}
