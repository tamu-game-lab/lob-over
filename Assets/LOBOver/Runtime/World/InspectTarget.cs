using UnityEngine;

namespace LOBOver.World
{
    // Component lookup avoids adding a project tag (ProjectSettings is outside WO-S3).
    public sealed class InspectTarget : MonoBehaviour
    {
        public string target;
        public static void Attach(GameObject obj, string value)
        {
            var marker = obj.GetComponent<InspectTarget>() ?? obj.AddComponent<InspectTarget>(); marker.target = value;
        }
    }
}
