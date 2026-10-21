using LOBOver.Sim;
using TMPro;
using UnityEngine;

namespace LOBOver.World
{
    public sealed class StationCell : MonoBehaviour
    {
        public GameObject surface, floor, walls, pipes, wetBand, water, slump, box, shoring;
        public Transform nearWall, farWall, stake;
        public TextMeshPro stationText;
        public Material openMaterial, pipeMaterial, backfillMaterial, surfaceMaterial;
        public string State { get; private set; }
        public string Water { get; private set; }
        public string Wall { get; private set; }
        public string Protection { get; private set; }
        public double StationFt { get; private set; }
        private AudioSource sound;
        private WorldAssets assets;
        private string lastKey;

        public void Initialize(double feet, WorldAssets source)
        {
            StationFt = feet; assets = source; name = "Cell_" + WorldUnits.Station(feet);
            transform.localPosition = new Vector3((float)feet * WorldUnits.MetresPerFoot, 0, 0);
            stationText.text = WorldUnits.Station(feet); sound = WorldDrawing.Sound(gameObject, false);
        }
        public void Apply(TrenchView view)
        {
            var key = view.state + "/" + view.wall + "/" + view.water + "/" + view.protection + "/" + view.pipeDamage;
            if (key == lastKey) return;
            if (lastKey != null && Application.isPlaying && sound.isActiveAndEnabled)
            {
                if (view.wall == "slumped" && Wall != "slumped") sound.PlayOneShot(assets.collapse);
                if (view.state == "piped" && State != "piped") sound.PlayOneShot(assets.pipeSetdown);
            }
            State = view.state; Water = view.water; Wall = view.wall; Protection = view.protection; lastKey = key;
            var dug = State == "open" || State == "piped";
            surface.SetActive(!dug); floor.SetActive(dug); walls.SetActive(dug); pipes.SetActive(State == "piped");
            surface.GetComponent<Renderer>().sharedMaterial = State == "backfilled" ? backfillMaterial : surfaceMaterial;
            floor.GetComponent<Renderer>().sharedMaterial = State == "piped" ? pipeMaterial : openMaterial;
            wetBand.SetActive(dug && Water == "seep"); water.SetActive(dug && (Water == "standing" || Wall == "slumped"));
            slump.SetActive(dug && Wall == "slumped"); box.SetActive(dug && Protection == "box"); shoring.SetActive(dug && Protection == "shoring");
            nearWall.localRotation = Quaternion.Euler(Protection == "slope" ? 30 : 0, 0, 0);
            farWall.localRotation = Quaternion.Euler(Protection == "slope" ? -30 : 0, 0, 0);
            Debug.Log(name + " " + key, this);
        }
    }
}
