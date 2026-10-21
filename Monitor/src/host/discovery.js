import { createSocket } from 'node:dgram';
import { networkInterfaces, hostname } from 'node:os';

// WO-L1 discovery protocol and real-time beacon cadence (outside the simulation).
export const DISCOVERY_PORT = 7332;
const BEACON_MS = 2000;
export const ipv4Interfaces = () => Object.values(networkInterfaces()).flat()
  .filter(entry => entry.family === 'IPv4' && !entry.internal);
const broadcastAddress = ({ address, netmask }) => address.split('.').map((part, i) =>
  Number(part) | (255 ^ Number(netmask.split('.')[i]))).join('.');

export async function startDiscovery(port, { interfaces = ipv4Interfaces(), name = hostname(),
  destinationPort = DISCOVERY_PORT } = {}) {
  const packet = Buffer.from(`LOBOVER 1 ${port} ${name}`);
  const sockets = [];
  for (const entry of interfaces) {
    const socket = createSocket('udp4');
    socket.on('error', error => console.error(`Discovery ${entry.address}: ${error.message}`));
    const bound = await new Promise(resolve => {
      socket.once('error', () => resolve(false));
      socket.bind(0, entry.address, () => resolve(true));
    });
    if (!bound) { socket.close(); continue; }
    socket.setBroadcast(true);
    sockets.push({ socket, address: broadcastAddress(entry) });
  }
  const send = () => {
    for (const { socket, address } of sockets) socket.send(packet, destinationPort, address);
  };
  send();
  const timer = setInterval(send, BEACON_MS);
  return { close() { clearInterval(timer); for (const { socket } of sockets) socket.close(); } };
}
