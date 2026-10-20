using System.Collections.Generic;
using LOBOver.Sim;
using LOBOver.VR;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace LOBOver.Tests
{
    public sealed class InputLayerTests
    {
        private InspectorInputLayer layer;
        private FakeInput input;
        private List<JObject> commands;
        private string text;
        [SetUp]
        public void Setup()
        {
            layer = new InspectorInputLayer(); input = new FakeInput(); commands = new List<JObject>();
            layer.Command += commands.Add; layer.Text += value => text = value;
            layer.Observe(new TruckView { stationFt = 520, dismounted = true });
        }
        [Test]
        public void DwellSendsOnceAtTwoSecondsAndEnforcesPerTargetCooldown()
        {
            Tick(1.99f, 1.99f, "wall"); Assert.That(commands.Count, Is.Zero);
            Tick(0.01f, 2, "wall"); Assert.That(commands.Count, Is.EqualTo(1));
            Tick(2, 4, "wall"); Assert.That(commands.Count, Is.EqualTo(1));
            Tick(0, 4, null); Tick(2, 5.99f, "wall"); Assert.That(commands.Count, Is.EqualTo(1));
            Tick(0.01f, 6, "wall"); Assert.That(commands.Count, Is.EqualTo(2));
            Assert.That((string)commands[0]["action"], Is.EqualTo("inspect"));
        }
        [Test]
        public void MountedButtonIsRefusedWithClipboardText()
        {
            layer.Observe(new TruckView { stationFt = 520 }); input.Button = InspectorButton.Flag;
            Tick(0, 0, null); Assert.That(commands, Is.Empty); Assert.That(text, Is.EqualTo("dismount first"));
        }
        [Test]
        public void FlagSendsCurrentStakeAndEmptyNote()
        {
            input.Button = InspectorButton.Flag; Tick(0, 0, null);
            Assert.That(commands.Count, Is.EqualTo(1));
            Assert.That(JToken.DeepEquals(commands[0], new JObject { ["action"] = "flag", ["stationFt"] = 520.0, ["note"] = "" }), Is.True);
        }
        [Test]
        public void RideNeverMovesCabBeforeSettledStateArrives()
        {
            layer.Observe(new TruckView { stationFt = 0 }); layer.Ride(520);
            Assert.That((double)commands[0]["stationFt"], Is.EqualTo(520)); Assert.That(layer.CabStation, Is.Zero);
            layer.Observe(new TruckView { stationFt = 270, moving = true }); Assert.That(layer.CabStation, Is.Zero);
            layer.Observe(new TruckView { stationFt = 520, moving = false }); Assert.That(layer.CabStation, Is.EqualTo(520));
        }
        private void Tick(float dt, float now, string focus) => layer.Tick(input, dt, now, focus, focus, 1, null);
    }
}
