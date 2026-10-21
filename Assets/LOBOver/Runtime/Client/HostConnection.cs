using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LOBOver.Sim;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace LOBOver.Client
{
    public sealed class HostConnection : MonoBehaviour
    {
        // Empty in production; the PlayMode host uses its own port without changing discovery.
        [NonSerialized] public string EndpointOverride;
        public static string TestEndpointOverride;
        public bool Connected { get; private set; }
        public bool Mirror { get; private set; }
        public string Hostname { get; private set; } = "PC";
        public string ScenarioId { get; private set; }
        public string RunId { get; private set; }
        public SiteView View { get; private set; }
        public JObject Job { get; private set; }
        public readonly Dictionary<string, bool> CrewPresence = new Dictionary<string, bool>();
        public int DisplayDay { get; private set; } = 1;
        public bool Started { get; private set; }
        public event Action Changed;
        public event Action<JObject> Message;
        public event Action<JObject> CommandSent;
        private readonly ConcurrentQueue<Action> main = new ConcurrentQueue<Action>();
        private CancellationTokenSource lifetime;
        private ClientWebSocket socket;
        private readonly SemaphoreSlim sending = new SemaphoreSlim(1, 1);
        private int commandNumber;
        private bool listening;
#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject multicastLock;
#endif
        private void OnEnable()
        {
            if (TestEndpointOverride != null) EndpointOverride = TestEndpointOverride;
            lifetime = new CancellationTokenSource();
            _ = ConnectLoop(lifetime.Token);
        }
        private void Update() { while (main.TryDequeue(out var action)) action(); }
        private void Queue(CancellationToken token, Action action) => main.Enqueue(() =>
        { if (!token.IsCancellationRequested && this != null && isActiveAndEnabled) action(); });
        private void OnDisable()
        {
            lifetime?.Cancel(); socket?.Abort(); SetListening(false);
            Connected = false; Changed?.Invoke();
        }
        private async Task ConnectLoop(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var endpoint = EndpointOverride;
                    ClientWebSocket connected = await TryConnect(endpoint ?? "ws://127.0.0.1:7331/ws", token);
                    var hostname = endpoint == null ? "PC" : new Uri(endpoint).Host;
                    while (connected == null && !token.IsCancellationRequested)
                    {
                        if (endpoint != null)
                        { await Task.Delay(DiscoveryPacket.RetryMs, token); connected = await TryConnect(endpoint, token); continue; }
                        var found = await Discover(token);
                        if (found != null)
                        { hostname = found.Item2; connected = await TryConnect(found.Item1, token); }
                        // Discovery times out every two seconds, permitting a newly attached USB cable.
                        else connected = await TryConnect("ws://127.0.0.1:7331/ws", token);
                    }
                    if (connected == null) break;
                    socket = connected;
                    Queue(token, () => Hostname = hostname);
                    try
                    {
                        await SendText(connected, new JObject { ["t"] = "hello", ["v"] = 1, ["seat"] = "site", ["client"] = "quest" }, token);
                        await Receive(connected, token);
                    }
                    catch (Exception error) when (error is WebSocketException || error is IOException || error is OperationCanceledException) { }
                    finally { connected.Dispose(); if (socket == connected) socket = null; }
                    Queue(token, () => { Connected = false; Changed?.Invoke(); });
                    await Task.Delay(DiscoveryPacket.RetryMs, token);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { Queue(token, () => Debug.LogWarning("LOB Over connection: " + error.Message)); }
            finally { Queue(token, () => SetListening(false)); }
        }
        private static async Task<ClientWebSocket> TryConnect(string address, CancellationToken token)
        {
            var candidate = new ClientWebSocket();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(DiscoveryPacket.UsbTimeoutMs);
            try { await candidate.ConnectAsync(new Uri(address), timeout.Token); return candidate; }
            catch (Exception error) when (error is WebSocketException || error is OperationCanceledException || error is SocketException)
            { candidate.Dispose(); return null; }
        }
        private async Task<Tuple<string, string>> Discover(CancellationToken token)
        {
            Queue(token, () => SetListening(true));
            try
            {
                using var udp = new UdpClient(AddressFamily.InterNetwork);
                udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udp.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPacket.DiscoveryPort));
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                timeout.CancelAfter(DiscoveryPacket.RetryMs);
                using var registration = timeout.Token.Register(() => udp.Close());
                while (!timeout.IsCancellationRequested)
                {
                    var packet = await udp.ReceiveAsync();
                    if (DiscoveryPacket.TryParse(Encoding.UTF8.GetString(packet.Buffer), out var port, out var name))
                        return Tuple.Create("ws://" + packet.RemoteEndPoint.Address + ":" + port + "/ws", name);
                }
            }
            catch (Exception error) when (error is SocketException || error is ObjectDisposedException) { }
            finally { Queue(token, () => SetListening(false)); }
            return null;
        }
        private void SetListening(bool value)
        {
            if (listening == value) return;
            listening = value;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (value)
            {
                using var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unity.GetStatic<AndroidJavaObject>("currentActivity");
                using var context = activity.Call<AndroidJavaObject>("getApplicationContext");
                using var wifi = context.Call<AndroidJavaObject>("getSystemService", "wifi");
                multicastLock = wifi.Call<AndroidJavaObject>("createMulticastLock", "LOBOver discovery");
                multicastLock.Call("setReferenceCounted", false); multicastLock.Call("acquire");
            }
            else if (multicastLock != null)
            { multicastLock.Call("release"); multicastLock.Dispose(); multicastLock = null; }
#endif
        }
        private async Task Receive(ClientWebSocket connection, CancellationToken token)
        {
            var buffer = new byte[8192];
            while (connection.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                using var bytes = new MemoryStream();
                WebSocketReceiveResult frame;
                do
                {
                    frame = await connection.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                    if (frame.MessageType == WebSocketMessageType.Close) return;
                    bytes.Write(buffer, 0, frame.Count);
                } while (!frame.EndOfMessage);
                if (frame.MessageType != WebSocketMessageType.Text) continue;
                var text = Encoding.UTF8.GetString(bytes.ToArray());
                Queue(token, () => Accept(text));
            }
        }
        private void Accept(string text)
        {
            try
            {
                var message = JObject.Parse(text);
                if ((int?)message["v"] != 1) return;
                var type = (string)message["t"];
                if (type == "welcome")
                {
                    if ((string)message["seat"] != "site") return;
                    ScenarioId = (string)message["stage"]; Mirror = (bool?)message["mirror"] ?? false;
                    Hostname = (string)message["hostname"] ?? Hostname;
                    Connected = true;
                    ApplyState(message["state"] as JObject);
                }
                else if (type == "state") ApplyState(message);
                Message?.Invoke(message);
                Changed?.Invoke();
            }
            catch (JsonException error) { Debug.LogWarning("LOB Over host message: " + error.Message); }
        }
        private void ApplyState(JObject message)
        {
            if (!(message?["view"] is JObject view) || !(view["crews"] is JArray) || !(view["project"]?["job"] is JObject job)) return;
            var run = (string)message["runId"];
            if (RunId != run) Started = false;
            RunId = run;
            ScenarioId = (string)message["stage"] ?? ScenarioId;
            DisplayDay = (int?)message["displayDay"] ?? (int)Math.Floor((double?)message["day"] ?? 0) + 1;
            Started |= (double?)message["day"] > 0 || (string)message["speed"] != "paused";
            Job = job; View = SiteView.From(view);
            CrewPresence.Clear();
            foreach (var crew in (JArray)view["crews"])
                CrewPresence[(string)crew["id"]] = (bool?)crew["onSite"] ?? true;
        }
        public async Task<bool> SendCommand(string action, double? stationFt = null)
        {
            if (!Connected || Mirror || socket == null) return false;
            var message = new JObject { ["t"] = "cmd", ["v"] = 1, ["id"] = "quest-" + ++commandNumber, ["seat"] = "site", ["action"] = action };
            if (stationFt.HasValue) message["stationFt"] = stationFt.Value;
            try
            {
                await SendText(socket, message, lifetime.Token);
                CommandSent?.Invoke(message); return true;
            }
            catch (Exception error) when (error is WebSocketException || error is OperationCanceledException || error is ObjectDisposedException)
            { return false; }
        }
        private async Task SendText(ClientWebSocket connection, JObject message, CancellationToken token)
        {
            await sending.WaitAsync(token);
            try
            {
                var bytes = Encoding.UTF8.GetBytes(message.ToString(Formatting.None));
                await connection.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, token);
            }
            finally { sending.Release(); }
        }
    }
}
