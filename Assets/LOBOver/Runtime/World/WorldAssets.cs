using System;
using TMPro;
using UnityEngine;

namespace LOBOver.World
{
    [Serializable] public sealed class MachineAsset
    {
        public string crew, plate, label;
        public GameObject prefab;
    }
    public sealed class WorldAssets : ScriptableObject
    {
        public StationCell cellPrefab;
        public CrewRig crewPrefab;
        public GameObject worker, particles, spoil, barrier, cone;
        public MachineAsset[] machines;
        public TMP_FontAsset font;
        public Material ink, soil, timber, steel, orange, cloud, rain;
        // WO-L4 assets live under Art so rebuilding Play never rewrites the legacy prefabs.
        public bool detailedSite;
        public GameObject pipe, fence, jersey, tree;
        public Material ground, road, trench, bedding, spoilSurface, wetSoil, waterSurface, backfill, flagging, siteText, cabin, glass, skyMaterial;
        public Mesh groundMesh, horizonMesh, wallNearMesh, wallFarMesh, stripMesh, slumpMesh, windrowMesh, blockMesh;
        public AudioClip wind, rainfall, excavation, dozer, idle, conflict, collapse, pipeSetdown;
        public MachineAsset Machine(string crew, string plate) => Array.Find(machines,
            item => item.crew == crew && string.Equals(item.plate, plate, StringComparison.OrdinalIgnoreCase));
    }

    public static class WorldUnits
    {
        // WO-08 sections 1-3: physical scale and station geometry.
        public const float MetresPerFoot = 0.3048f, StationFeet = 100, CellLength = 30.48f;
        public const float TrenchWidth = 1.2f, TrenchDepth = 1.8f, RoadZ = 6;
        public const float PoseSeconds = 0.5f, DemobBackMetres = 60;
        public static readonly string[] CrewIds = { "exc", "pipe", "back" };
        // Crew identity colours, never status or decision feedback.
        public static readonly Color[] CrewColours = { new Color(0.95f, 0.65f, 0.25f), new Color(0.4f, 0.7f, 1), new Color(0.85f, 0.65f, 0.95f) };
        public static string Station(double feet) => ((int)Math.Floor(feet / 100)) + "+" + ((int)Math.Round(feet % 100)).ToString("00");
    }

    public static class WorldDrawing
    {
        public static GameObject Group(string name, Transform parent)
        {
            var obj = new GameObject(name); obj.transform.SetParent(parent, false); return obj;
        }
        public static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size, Material material)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name = name;
            obj.transform.SetParent(parent, false); obj.transform.localPosition = position; obj.transform.localScale = size;
            obj.GetComponent<Renderer>().sharedMaterial = material; return obj;
        }
        public static TextMeshPro Text(string name, Transform parent, Vector3 position, Vector2 size, float height, TMP_FontAsset font)
        {
            var obj = Group(name, parent); obj.transform.localPosition = position;
            var text = obj.AddComponent<TextMeshPro>(); text.font = font;
            text.fontSize = height * 10; text.color = new Color(0.95f, 0.94f, 0.9f);
            text.alignment = TextAlignmentOptions.TopLeft; text.rectTransform.sizeDelta = size;
            text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Overflow;
            text.richText = false; return text;
        }
        public static LineRenderer Line(string name, Transform parent, Material material, Color colour, float width)
        {
            var line = Group(name, parent).AddComponent<LineRenderer>(); line.useWorldSpace = false;
            line.sharedMaterial = material; line.startColor = line.endColor = colour;
            line.startWidth = line.endWidth = width; line.positionCount = 0;
            line.alignment = LineAlignment.TransformZ; return line;
        }
        public static AudioSource Sound(GameObject obj, bool loop, float distance = 50)
        {
            var audio = obj.AddComponent<AudioSource>(); audio.playOnAwake = false; audio.loop = loop;
            audio.spatialBlend = 1; audio.rolloffMode = AudioRolloffMode.Linear; audio.minDistance = 2; audio.maxDistance = distance;
            return audio;
        }
        public static void Loop(AudioSource audio, AudioClip clip)
        {
            if (audio.clip == clip) return;
            audio.Stop(); audio.clip = clip;
            if (clip != null && Application.isPlaying && audio.isActiveAndEnabled) audio.Play();
        }
        public static void Remove(GameObject obj)
        {
            if (obj == null) return;
            obj.SetActive(false);
            if (Application.isPlaying) UnityEngine.Object.Destroy(obj); else UnityEngine.Object.DestroyImmediate(obj);
        }
    }
}
