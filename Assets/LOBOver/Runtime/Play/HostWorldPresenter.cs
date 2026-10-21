using LOBOver.Client;
using LOBOver.World;
using UnityEngine;

namespace LOBOver.Play
{
    public sealed class HostWorldPresenter : MonoBehaviour
    {
        public HostConnection connection;
        public SiteWorld world;
        public bool Frozen { get; set; }
        private string configuredRun, configuredStage;
        private object appliedView;
        private void Update() => Present();
        public void Present()
        {
            if (Frozen || connection.View == null || connection.Job == null) return;
            if (world.CurrentView == null || configuredRun != connection.RunId || configuredStage != connection.ScenarioId)
            {
                // Geometry comes from the host's public Site project, never a local scenario/simulator.
                world.Configure(connection.Job, 0);
                configuredRun = connection.RunId; configuredStage = connection.ScenarioId;
                appliedView = null;
            }
            if (ReferenceEquals(appliedView, connection.View)) return;
            world.Apply(connection.View, connection.DisplayDay);
            foreach (var crew in connection.View.crews)
                if (connection.CrewPresence.TryGetValue(crew.id, out var present)) world.Crew(crew.id).gameObject.SetActive(present);
            appliedView = connection.View;
        }
    }
}
