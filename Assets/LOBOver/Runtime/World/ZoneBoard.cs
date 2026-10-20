using LOBOver.Sim;
using TMPro;
using UnityEngine;

namespace LOBOver.World
{
    public sealed class ZoneBoard : MonoBehaviour
    {
        public TextMeshPro label;
        public int Zone { get; private set; }
        public string Text => label.text;
        public void Initialize(int zone) { Zone = zone; name = "ZoneBoard_" + zone; }
        public void Apply(BoardView view)
        {
            label.text = "ZONE " + Zone + "\n" + (view.permitNumber ?? "no permit posted")
                + (string.IsNullOrEmpty(view.inspectionNumber) ? "" : "\n" + view.inspectionNumber);
        }
    }
}
