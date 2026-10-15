import { WebSocket, WebSocketServer } from 'ws';

const SEATS = new Set(['plan', 'site', 'facilitator']);
export function createClients(server, { state, welcome, command }) {
  const wss = new WebSocketServer({ server, path: '/ws' });
  const clients = new Map();
  const send = (socket, message) => {
    if (socket.readyState === WebSocket.OPEN) socket.send(JSON.stringify(message));
  };
  const broadcast = () => {
    const views = new Map();
    for (const [socket, client] of clients) {
      if (!views.has(client.seat)) views.set(client.seat, state(client.seat));
      send(socket, views.get(client.seat));
    }
  };
  wss.on('connection', socket => {
    socket.on('error', error => console.error('WebSocket:', error.message));
    socket.on('close', () => clients.delete(socket));
    socket.on('message', raw => {
      let message;
      try { message = JSON.parse(raw); } catch { return; }
      if (!message || message.v !== 1) return;
      if (message.t === 'hello') {
        if (!SEATS.has(message.seat) || clients.has(socket)) return;
        const mirror = [...clients.values()].some(c => c.seat === message.seat && !c.mirror);
        clients.set(socket, { seat: message.seat, mirror });
        send(socket, { t: 'welcome', v: 1, protocol: 1, seat: message.seat, mirror,
          stage: state(message.seat).stage, state: state(message.seat) });
        welcome(socket, message.seat, send);
        return;
      }
      if (message.t !== 'cmd' && message.t !== 'ctrl') {
        console.warn('Unknown message type:', message.t);
        return;
      }
      const client = clients.get(socket);
      let reason;
      if (!client) reason = 'hello required';
      else if (client.mirror) reason = 'mirror';
      else if (message.t === 'ctrl' && client.seat !== 'facilitator'
        || message.t === 'cmd' && (message.seat !== client.seat || client.seat === 'facilitator')) {
        reason = 'action unavailable for seat';
      }
      if (reason) { send(socket, { t: 'nack', v: 1, id: message.id, reason }); return; }
      command(socket, client.seat, message, send);
    });
  });
  return {
    broadcast,
    deliver(message, requester) {
      for (const [socket, client] of clients) {
        if (message.t === 'inspection') {
          if (socket === requester) send(socket, message);
        } else {
          const seats = message.t === 'report' ? ['plan', 'facilitator']
            : message.t === 'result' ? [...SEATS] : message.seats ?? [];
          if (seats.includes(client.seat)) send(socket, message);
        }
      }
    },
    close() { for (const socket of wss.clients) socket.terminate(); wss.close(); },
  };
}
