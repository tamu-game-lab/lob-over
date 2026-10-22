import { DurableObject } from 'cloudflare:workers';

// WO-L9: six characters, omitting ambiguous I, O, 0 and 1.
const CODE = /^[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{6}$/;
const SEATS = new Set(['site', 'plan', 'rules', 'facilitator']);
const CLIENTS = new Set(['quest', 'web']);
const directory = env => env.DIRECTORY.getByName('rooms');
const room = (env, code) => env.ROOMS.getByName(code);
const open = socket => socket.readyState === WebSocket.OPEN;
const json = value => Response.json(value, { headers: {
  'Access-Control-Allow-Origin': '*', 'Cache-Control': 'no-store',
} });
function parse(raw) {
  if (typeof raw !== 'string') return null;
  try { return JSON.parse(raw); } catch { return null; }
}

export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    if (request.method !== 'GET') return new Response('Method not allowed', { status: 405 });
    if (url.pathname === '/rooms') return json(await directory(env).list());
    const match = /^\/(host|join)\/([^/]+)$/.exec(url.pathname);
    if (!match || !CODE.test(match[2])) return new Response('Unknown room path', { status: 404 });
    if (request.headers.get('Upgrade')?.toLowerCase() !== 'websocket') {
      return new Response('WebSocket required', { status: 426 });
    }
    if (match[1] === 'join' && (!SEATS.has(url.searchParams.get('seat')) || !CLIENTS.has(url.searchParams.get('client')))) {
      return new Response('Unknown seat or client', { status: 400 });
    }
    return room(env, match[2]).fetch(request);
  },
};

// Host sockets double as the directory entries. Their attachments survive
// hibernation; an in-memory Map alone would silently forget idle open rooms.
// Both DOs accept incoming sockets, so neither needs an outbound socket/timer
// that would prevent hibernation. No Storage API, KV or message history is used.
export class Directory extends DurableObject {
  host(code) { return this.ctx.getWebSockets(code).find(socket => open(socket) && !socket.deserializeAttachment().closed); }
  list() {
    return this.ctx.getWebSockets().filter(socket => open(socket) && !socket.deserializeAttachment().closed).map(socket => {
      const { code, host, since } = socket.deserializeAttachment();
      return { code, host, since };
    }).sort((a, b) => a.code.localeCompare(b.code));
  }
  hostInfo(code) { return this.host(code)?.deserializeAttachment() ?? null; }
  fetch(request) {
    const url = new URL(request.url), code = url.pathname.split('/').at(-1);
    if (this.host(code)) return new Response('Room already has a host', { status: 409 });
    const pair = new WebSocketPair();
    this.ctx.acceptWebSocket(pair[1], [code]);
    pair[1].serializeAttachment({ code, host: url.searchParams.get('name') || 'Browser',
      since: Date.now(), session: crypto.randomUUID() });
    return new Response(null, { status: 101, webSocket: pair[0] });
  }
  send(code, session, message) {
    const socket = this.host(code);
    if (!socket || socket.deserializeAttachment().session !== session) return false;
    socket.send(JSON.stringify(message));
    return true;
  }
  async webSocketMessage(socket, raw) {
    const message = parse(raw);
    if (!message || message.t !== 'relay' || typeof message.to !== 'string' || !('data' in message)) return;
    const { code, session } = socket.deserializeAttachment();
    await room(this.env, code).deliver(session, message.to, message.data);
  }
  async webSocketClose(socket) {
    const attachment = socket.deserializeAttachment();
    // workerd can still report OPEN after close; the attachment owns liveness.
    socket.serializeAttachment({ ...attachment, closed: true });
    socket.close();
    const { code, session } = attachment;
    await room(this.env, code).hostLeft(session);
  }
  async webSocketError(socket) { await this.webSocketClose(socket); }
}

export class Room extends DurableObject {
  async fetch(request) {
    const url = new URL(request.url), code = url.pathname.split('/').at(-1);
    // Rooms register their host with Directory, which owns the socket and list
    // entry together. A second host is rejected atomically in that object.
    if (url.pathname.startsWith('/host/')) return directory(this.env).fetch(request);
    const host = await directory(this.env).hostInfo(code);
    if (!host) return new Response('Room has no host', { status: 404 });
    const pair = new WebSocketPair();
    const client = { code, session: host.session, id: crypto.randomUUID(),
      seat: url.searchParams.get('seat'), client: url.searchParams.get('client') };
    this.ctx.acceptWebSocket(pair[1]);
    pair[1].serializeAttachment(client);
    if (!await this.notify(client, { event: 'join' })) pair[1].close(1000, 'host left');
    return new Response(null, { status: 101, webSocket: pair[0] });
  }
  notify({ code, session, id, seat, client }, fields) {
    return directory(this.env).send(code, session, { t: 'relay', from: id, seat, client, ...fields });
  }
  deliver(session, to, data) {
    const raw = JSON.stringify(data);
    for (const socket of this.ctx.getWebSockets()) {
      const client = socket.deserializeAttachment();
      if (open(socket) && client.session === session && (to === '*' || to === client.id)) socket.send(raw);
    }
  }
  hostLeft(session) {
    for (const socket of this.ctx.getWebSockets()) {
      const client = socket.deserializeAttachment();
      if (open(socket) && client.session === session) {
        socket.serializeAttachment({ ...client, closing: true });
        socket.close(1000, 'host left');
      }
    }
    // Entries disappear immediately on close: no hostless entry can linger
    // past WO-L9's ten-minute expiry, and /rooms only lists open host sockets.
  }
  async webSocketMessage(socket, raw) {
    const data = parse(raw);
    if (!data || typeof data !== 'object' || Array.isArray(data)) return;
    if (!await this.notify(socket.deserializeAttachment(), { data })) socket.close(1000, 'host left');
  }
  async webSocketClose(socket) {
    const client = socket.deserializeAttachment();
    if (client.left) return;
    socket.serializeAttachment({ ...client, left: true });
    // hostLeft already started the close handshake for these seats.
    if (!client.closing) socket.close();
    await this.notify(client, { event: 'leave' });
  }
  async webSocketError(socket) { await this.webSocketClose(socket); }
}
