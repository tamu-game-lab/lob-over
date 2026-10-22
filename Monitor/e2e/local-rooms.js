import { createServer } from 'node:http';
import { once } from 'node:events';
import { readFile } from 'node:fs/promises';
import { WebSocketServer, WebSocket } from 'ws';

// Only signaling is local: the vendored PeerJS and real WebRTC data channels run
// unchanged across isolated browser contexts, without the public room service.
export async function localRooms() {
  const server = createServer((req, res) => res.writeHead(404).end());
  const wss = new WebSocketServer({ server });
  const peers = new Map(), forwarded = [];
  wss.on('connection', (socket, request) => {
    const id = new URL(request.url, 'http://localhost').searchParams.get('id');
    if (!id || peers.has(id)) { socket.send(JSON.stringify({ type: 'ID-TAKEN' })); socket.close(); return; }
    peers.set(id, socket);
    socket.send(JSON.stringify({ type: 'OPEN' }));
    socket.on('message', raw => {
      const message = JSON.parse(raw);
      if (!['OFFER', 'ANSWER', 'CANDIDATE'].includes(message.type)) return;
      const target = peers.get(message.dst);
      if (target?.readyState === WebSocket.OPEN) {
        forwarded.push(message.type);
        target.send(JSON.stringify({ ...message, src: id }));
      } else socket.send(JSON.stringify({ type: 'EXPIRE', src: message.dst }));
    });
    socket.on('close', () => { if (peers.get(id) === socket) peers.delete(id); });
  });
  server.listen(0, '127.0.0.1');
  await once(server, 'listening');
  const port = server.address().port;
  const peerSource = await readFile(new URL('../web/lib/peerjs.min.js', import.meta.url), 'utf8');
  const configuredPeer = `${peerSource}\n;globalThis.Peer = class extends globalThis.Peer {
    constructor(id) { super(id || crypto.randomUUID(), {
      host: '127.0.0.1', port: ${port}, path: '/', secure: false, config: { iceServers: [] }
    }); }
  };`;
  return {
    forwarded,
    async configure(context) {
      await context.route('**/lib/peerjs.min.js', route => route.fulfill({ contentType: 'text/javascript', body: configuredPeer }));
    },
    async close() {
      for (const socket of wss.clients) socket.terminate();
      await new Promise(resolve => wss.close(resolve));
      await new Promise(resolve => server.close(resolve));
    },
  };
}
