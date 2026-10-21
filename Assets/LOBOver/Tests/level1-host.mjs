// Real Node host on the integration-test port. Test-only voice delivery stays outside Monitor/.
import { createServer } from 'node:http';
import { createRequire } from 'node:module';
import { readFile } from 'node:fs/promises';
import { createInterface } from 'node:readline';
import { createHost } from '../../../Monitor/src/host/core.js';
import { loadCatalog } from '../../../Monitor/src/common/catalog.js';
import { loadScenario } from '../../../Monitor/src/scenario/load.js';
const require = createRequire(new URL('../../../Monitor/package.json', import.meta.url));
const { WebSocketServer, WebSocket } = require('ws');
const catalog = loadCatalog();
const scenario = loadScenario('Data/Scenarios/level1_texas.json', { catalog });
const core = createHost({ scenario, catalog });
const server = createServer();
const wss = new WebSocketServer({ server, path: '/ws' });
wss.on('connection', socket => {
  core.attach({
    send(message) { if (socket.readyState === WebSocket.OPEN) socket.send(JSON.stringify(message)); },
    onMessage(receive) { socket.on('message', raw => {
      const message = JSON.parse(raw);
      if (message.t === 'hello' && (message.seat !== 'site' || message.client !== 'quest')) throw new Error('Expected site/quest hello');
      receive(message);
    }); },
    onClose(close) { socket.on('close', close); }, close() { socket.close(); },
  });
});
await new Promise(resolve => server.listen(7333, '127.0.0.1', resolve));
const input = createInterface({ input: process.stdin });
input.on('line', async line => {
  if (line !== 'exchange') return;
  const scenario = JSON.parse(await readFile('Data/Scenarios/level1_texas.json', 'utf8'));
  const exchange = scenario.lines.find(item => item.id === 'grumble_back_pipe');
  const turns = exchange.turns.map((turn, index) => ({ ...turn, clip: `${exchange.id}_${index + 1}.wav`, stationFt: 0 }));
  const message = JSON.stringify({ t: 'voice', v: 1, seats: ['site'], lineIds: [exchange.id], lines: [exchange], turns });
  for (const socket of wss.clients) { socket.send(message); socket.send(message); }
});
console.log('Level1 test host listening on 7333');
