using System;
using System.Globalization;

namespace LOBOver.Client
{
    public static class DiscoveryPacket
    {
        // WO-L2 / WO-L1 discovery wire format. The address always comes from the UDP sender.
        public const int HostPort = 7331, DiscoveryPort = 7332;
        public const int UsbTimeoutMs = 1000, RetryMs = 2000;
        public static bool TryParse(string packet, out int port, out string hostname)
        {
            port = 0; hostname = null;
            var fields = (packet ?? "").Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length != 4 || fields[0] != "LOBOVER" || fields[1] != "1"
                || !int.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out var value)
                || value < 1 || value > ushort.MaxValue || fields[3].IndexOfAny(new[] { '\r', '\n', '\t', '/', '\\' }) >= 0) return false;
            port = value; hostname = fields[3]; return true;
        }
    }
}
