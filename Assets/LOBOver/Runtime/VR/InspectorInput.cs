using System;
using System.Collections.Generic;
using LOBOver.Sim;
using Newtonsoft.Json.Linq;

namespace LOBOver.VR
{
    public enum InspectorButton { None, Flag, StopWork, Confirm, Done, NotCarriedOut }
    public interface IInspectorInput
    {
        bool TriggerDown { get; }
        bool GoDown { get; }
        bool MountDown { get; }
        bool GripHeld { get; }
        float Turn { get; }
        float Navigate { get; }
        InspectorButton Button { get; }
    }
    public sealed class OvrInput : IInspectorInput
    {
        public bool TriggerDown => OVRInput.GetDown(OVRInput.RawButton.RIndexTrigger);
        public bool GoDown => OVRInput.GetDown(OVRInput.RawButton.A);
        public bool MountDown => OVRInput.GetDown(OVRInput.RawButton.B);
        public bool GripHeld => OVRInput.Get(OVRInput.RawButton.RHandTrigger);
        public float Turn => OVRInput.Get(OVRInput.RawAxis2D.RThumbstick).x;
        public float Navigate => OVRInput.Get(OVRInput.RawAxis2D.RThumbstick).y;
        public InspectorButton Button => InspectorButton.None;
    }
    public sealed class FakeInput : IInspectorInput
    {
        public bool TriggerDown { get; set; }
        public bool GoDown { get; set; }
        public bool MountDown { get; set; }
        public bool GripHeld { get; set; }
        public float Turn { get; set; }
        public float Navigate { get; set; }
        public InspectorButton Button { get; set; }
    }

    // The input seam consumes Site state only. Tests advance its clock without a headset.
    public sealed class InspectorInputLayer
    {
        public const float DwellSeconds = 2, CooldownSeconds = 4; // WO-09 section 4.
        public event Action<JObject> Command;
        public event Action<string> Text;
        public event Action ConfirmRequested;
        public TruckView Truck { get; private set; }
        public double CabStation { get; private set; }
        public float DwellFraction => Math.Min(1, dwell / DwellSeconds);
        private string focused;
        private float dwell;
        private bool fired;
        private readonly Dictionary<string, float> lastInspection = new Dictionary<string, float>();

        public void Observe(TruckView truck)
        {
            Truck = truck;
            // The simulation interpolates stationFt during travel. Never expose that as cab motion.
            if (!truck.moving) CabStation = truck.stationFt;
        }
        public void Tick(IInspectorInput input, float dt, float now, string identity, string target, int zone, string orderId)
        {
            if (input.MountDown && Truck != null) Send(new JObject { ["action"] = Truck.dismounted ? "mount" : "dismount" });
            if (input.Button != InspectorButton.None) Press(input.Button, zone, orderId);
            if (identity != focused) { focused = identity; dwell = 0; fired = false; }
            if (identity == null || Truck == null || !Truck.dismounted || Truck.moving) { dwell = 0; return; }
            dwell += Math.Max(0, dt);
            if (fired || dwell < DwellSeconds || lastInspection.TryGetValue(identity, out var last) && now - last < CooldownSeconds) return;
            fired = true; lastInspection[identity] = now;
            Send(new JObject { ["action"] = "inspect", ["target"] = target });
        }
        public void Ride(double station)
        {
            if (Truck == null) return;
            if (Truck.moving || Truck.dismounted) { Text?.Invoke("mount before moving"); return; }
            Send(new JObject { ["action"] = "moveTo", ["stationFt"] = station });
        }
        public void Press(InspectorButton button, int zone, string orderId)
        {
            if (Truck == null || !Truck.dismounted || Truck.moving) { Text?.Invoke("dismount first"); return; }
            if (button == InspectorButton.Flag)
                Send(new JObject { ["action"] = "flag", ["stationFt"] = Truck.stationFt, ["note"] = "" });
            if (button == InspectorButton.StopWork) Send(new JObject { ["action"] = "stopWork", ["zone"] = zone });
            if (button == InspectorButton.Confirm)
            { if (orderId == null) Text?.Invoke("no order landed here"); else ConfirmRequested?.Invoke(); }
            if (button == InspectorButton.Done || button == InspectorButton.NotCarriedOut)
            {
                if (orderId == null) { Text?.Invoke("no order landed here"); return; }
                Send(new JObject { ["action"] = "confirm", ["orderId"] = orderId, ["outcome"] = button == InspectorButton.Done ? "done" : "notDone" });
            }
        }
        private void Send(JObject command) => Command?.Invoke(command);
    }
}
