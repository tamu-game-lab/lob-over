import { createHost } from '../src/host/core.js';
import { loadCatalogFromObject } from '../src/common/catalog.js';
import { loadScenarioFromObject } from '../src/scenario/load.js';
import { drawQR } from './qr.js';

export async function connectLevel1() {
  const $ = id => document.getElementById(id);
  async function json(path) {
    const response = await fetch(path);
    if (!response.ok) throw new Error(`${path}: ${response.status}`);
    return response.json();
  }
  const catalog = loadCatalogFromObject(await json('Data/catalog.json'));
  const scenario = loadScenarioFromObject(await json('Data/Scenarios/level1_texas.json'), catalog);
  const core = createHost({ scenario, catalog });
  const runId = crypto.randomUUID();
  // Use the slice's six-character room names and its existing PeerJS transport.
  const alphabet = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
  const roomCode = [...crypto.getRandomValues(new Uint8Array(6))]
    .map(value => alphabet[value % alphabet.length]).join('');
  const roomURL = new URL('site.html', location.href);
  roomURL.searchParams.set('room', roomCode);
  $('room-code').textContent = roomCode;
  $('room-link').href = roomURL.href;
  drawQR($('room-qr'), roomURL.href);
  const transport = LobTransport.host(core, { runId, roomCode,
    onRoom() { $('room-status').textContent = 'Room ready'; $('room-status').dataset.ready = 'true'; },
    onStatus(text) { $('room-status').textContent = text; $('room-status').dataset.ready = 'false'; },
  });
  const socket = LobTransport.direct(core);
  let relay, closed = false;
  const relayStatus = text => { $('relay-status').textContent = text; };
  // Relay discovery never delays the local simulation or the independent QR path.
  fetch('Data/relay.json', { signal: AbortSignal.timeout(5000) })
    .then(response => { if (!response.ok) throw new Error('Relay config unavailable'); return response.json(); })
    .then(config => {
      if (!closed) relay = LobTransport.relayHost(core, { url: config.relay, roomCode, onStatus: relayStatus });
    }).catch(() => { if (!closed) relayStatus('Relay: unavailable'); });
  addEventListener('pagehide', () => {
    closed = true; socket.close(); transport.close(); relay?.close(); core.close();
  }, { once: true });
  return socket;
}
