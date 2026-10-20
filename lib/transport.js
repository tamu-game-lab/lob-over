/* WO-S1: protocol-compatible sockets; PeerJS 1.5.4 is vendored beside this file. */
(() => {
  let peerLibrary;
  function loadPeer() {
    if (globalThis.Peer) return Promise.resolve();
    return peerLibrary ??= new Promise((resolve, reject) => {
      const script = document.createElement('script');
      script.src = new URL('lib/peerjs.min.js', document.baseURI);
      script.onload = resolve;
      script.onerror = () => reject(new Error('PeerJS library unavailable'));
      document.head.append(script);
    });
  }
  class Socket extends EventTarget {
    readyState = 0;
    emit(type, data) {
      const event = type === 'message' ? new MessageEvent(type, { data: JSON.stringify(data) }) : new Event(type);
      this['on' + type]?.(event);
      this.dispatchEvent(event);
    }
    opened() { if (this.readyState === 0) { this.readyState = 1; this.emit('open'); } }
    send(raw) { if (this.readyState === 1) this.write(JSON.parse(raw)); }
    close() {
      if (this.readyState === 3) return;
      this.readyState = 3; this.dispose?.(); this.emit('close');
    }
  }
  const channelName = run => 'lob-over-' + run;
  const peerId = room => 'lob-over-' + room.toUpperCase();
  function local(run) {
    const socket = new Socket(), id = crypto.randomUUID(), channel = new BroadcastChannel(channelName(run));
    const post = (kind, message) => channel.postMessage({ to: 'host', id, kind, message });
    const retry = setInterval(() => post('connect'), 500);
    socket.write = message => post('message', message);
    socket.dispose = () => { clearInterval(retry); post('close'); channel.close(); };
    channel.onmessage = ({ data }) => {
      if (data.to !== id) return;
      if (data.kind === 'connected') { clearInterval(retry); socket.opened(); }
      if (data.kind === 'message') socket.emit('message', data.message);
      if (data.kind === 'close') socket.close();
    };
    queueMicrotask(() => post('connect'));
    addEventListener('pagehide', () => socket.close(), { once: true });
    return socket;
  }
  function room(code) {
    const socket = new Socket();
    loadPeer().then(() => {
      if (socket.readyState === 3) return;
      const peer = new Peer();
      socket.dispose = () => peer.destroy();
      peer.on('error', () => socket.close());
      peer.on('open', () => {
        const connection = peer.connect(peerId(code), { reliable: true });
        socket.write = message => connection.send(message);
        connection.on('open', () => socket.opened());
        connection.on('data', message => socket.emit('message', message));
        connection.on('close', () => socket.close());
        connection.on('error', () => socket.close());
      });
    }).catch(() => socket.close());
    addEventListener('pagehide', () => socket.close(), { once: true });
    return socket;
  }
  function connect() {
    const query = new URLSearchParams(location.search);
    if (query.has('ws')) {
      const endpoint = query.get('ws').replace(/^wss?:\/\//, '').replace(/\/ws\/?$/, '');
      return new WebSocket(`${location.protocol === 'https:' ? 'wss' : 'ws'}://${endpoint}/ws`);
    }
    if (query.has('room')) return room(query.get('room'));
    const run = query.get('run') || localStorage.getItem('lob-over-run');
    if (run) return local(run);
    return new WebSocket(`${location.protocol === 'https:' ? 'wss' : 'ws'}://${location.host}/ws`);
  }
  function host(core, { runId, roomCode, onRoom, onStatus }) {
    const channel = new BroadcastChannel(channelName(runId)), connections = new Map();
    let peer, closed = false;
    channel.onmessage = ({ data }) => {
      if (data.to !== 'host') return;
      let connection = connections.get(data.id);
      if (data.kind === 'connect') {
        if (!connection) {
          connection = {
            send(message) { channel.postMessage({ to: data.id, kind: 'message', message }); },
            onMessage(callback) { this.receive = callback; },
            onClose(callback) { this.closed = callback; },
            close() {
              channel.postMessage({ to: data.id, kind: 'close' });
              this.closed(); connections.delete(data.id);
            },
          };
          connections.set(data.id, connection); core.attach(connection);
        }
        channel.postMessage({ to: data.id, kind: 'connected' });
      } else if (data.kind === 'message') connection?.receive(data.message);
      else if (data.kind === 'close') connection?.close();
    };
    loadPeer().then(() => {
      if (closed) return;
      peer = new Peer(peerId(roomCode));
      peer.on('open', () => onRoom(roomCode));
      peer.on('disconnected', () => onStatus('Room server disconnected'));
      peer.on('error', error => onStatus('Room unavailable: ' + error.type));
      peer.on('connection', connection => {
        // Install handlers before acknowledging the data-channel open.
        core.attach({
          send(message) { if (connection.open) connection.send(message); },
          onMessage(callback) { connection.on('data', callback); },
          onClose(callback) { connection.on('close', callback); },
          close() { connection.close(); },
        });
        connection.on('error', () => connection.close());
      });
    }).catch(error => onStatus(error.message));
    return { close() {
      closed = true;
      for (const connection of connections.values()) connection.close();
      channel.close(); peer?.destroy();
    } };
  }
  // The facilitator has the same message interface even though it owns the core.
  function direct(core) {
    const socket = new Socket();
    core.attach({
      send(message) { socket.emit('message', message); },
      onMessage(callback) { socket.write = callback; },
      onClose(callback) { socket.dispose = callback; },
      close() { socket.close(); },
    });
    setTimeout(() => socket.opened(), 0);
    return socket;
  }
  globalThis.LobTransport = { connect, host, direct };
})();
