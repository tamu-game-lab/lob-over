#if UNITY_EDITOR || !UNITY_ANDROID
// Legacy Site.unity remains available in the Editor; WO-L2 excludes this seat from Quest.
using UnityEngine;
namespace LOBOver.VR
{
    // Explicit, eye-locked comfort mask. No scene-load fade or world motion.
    public sealed class ComfortFade : MonoBehaviour
    {
        public Material material;
        private MeshRenderer blackout, vignette;
        private Mesh fullMesh, ringMesh;
        private MaterialPropertyBlock colour;
        private void Awake()
        {
            colour = new MaterialPropertyBlock();
            fullMesh = new Mesh { name = "Blackout" };
            fullMesh.vertices = new[] { new Vector3(-2, -2, 0.2f), new Vector3(2, -2, 0.2f), new Vector3(-2, 2, 0.2f), new Vector3(2, 2, 0.2f) };
            fullMesh.triangles = new[] { 0, 2, 1, 2, 3, 1 }; fullMesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            blackout = Surface("Blackout", fullMesh);
            const int edges = 64;
            var vertices = new Vector3[edges * 2]; var colours = new Color[edges * 2]; var indices = new int[edges * 6];
            for (var i = 0; i < edges; i++)
            {
                var angle = i * Mathf.PI * 2 / edges; var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
                vertices[i * 2] = direction * 0.12f + Vector3.forward * 0.19f;
                vertices[i * 2 + 1] = direction * 0.35f + Vector3.forward * 0.19f;
                colours[i * 2] = new Color(1, 1, 1, 0); colours[i * 2 + 1] = Color.white;
                var next = (i + 1) % edges * 2; var start = i * 6;
                indices[start] = i * 2; indices[start + 1] = next; indices[start + 2] = i * 2 + 1;
                indices[start + 3] = next; indices[start + 4] = next + 1; indices[start + 5] = i * 2 + 1;
            }
            ringMesh = new Mesh { name = "RideVignette", vertices = vertices, triangles = indices, colors = colours };
            vignette = Surface("Vignette", ringMesh); SetExplicitFade(0);
        }
        private MeshRenderer Surface(string name, Mesh mesh)
        {
            var obj = new GameObject(name); obj.transform.SetParent(transform, false);
            obj.AddComponent<MeshFilter>().sharedMesh = mesh; var renderer = obj.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false; return renderer;
        }
        public void SetExplicitFade(float value)
        {
            if (blackout == null) return;
            blackout.enabled = value > 0; colour.SetColor("_Color", new Color(0, 0, 0, value)); blackout.SetPropertyBlock(colour);
            // WO-09 section 7: vignette appears only during the fade, never during ordinary inspection.
            vignette.enabled = value > 0 && value < 1;
            colour.SetColor("_Color", new Color(0, 0, 0, Mathf.Sin(value * Mathf.PI))); vignette.SetPropertyBlock(colour);
        }
        private void OnDestroy() { if (fullMesh != null) Destroy(fullMesh); if (ringMesh != null) Destroy(ringMesh); }
    }
}

#endif
