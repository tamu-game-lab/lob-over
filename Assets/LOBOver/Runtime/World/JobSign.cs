using System.Linq;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;

namespace LOBOver.World
{
    public sealed class JobSign : MonoBehaviour
    {
        public TextMeshPro label;
        public void Apply(JObject sign) { label.text = string.Join("\n", sign.Properties().Select(field => field.Value.ToString())); }
    }
}
