import { createServer } from 'node:http';
import { readFile, stat } from 'node:fs/promises';
import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname, extname, isAbsolute, relative, resolve, sep } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { loadCatalog } from '../common/catalog.js';
import { loadScenario } from '../scenario/load.js';
import { WebSocketServer, WebSocket } from 'ws';
import { createHost, roundedJSON } from './core.js';

const ROOT = fileURLToPath(new URL('../../../', import.meta.url));
const WEB = resolve(ROOT, 'Monitor/web');
const VOICE = resolve(ROOT, 'Voice');
const OUT = resolve(ROOT, 'Monitor/out');
const MANUALS = resolve(ROOT, 'Paper/out');
// Docs/PROTOCOL.md default HTTP and WebSocket port.
const DEFAULT_PORT = 7331;
const MIME = { '.html': 'text/html; charset=utf-8', '.wav': 'audio/wav', '.ico': 'image/x-icon', '.js': 'text/javascript', '.json': 'application/json' };

export async function startHost({ scenarioPath, stage2Path, autoplanPath, guidePath, port = DEFAULT_PORT, testMode = process.env.NODE_ENV === 'test' }) {
  const catalog = loadCatalog();
  const base = process.env.INIT_CWD || process.cwd();
  const scenario = loadScenario(resolve(base, scenarioPath), { catalog });
  const stage2 = loadScenario(stage2Path ? resolve(base, stage2Path)
    : resolve(dirname(resolve(base, scenarioPath)), 'stage2_texas.json'), { catalog });
  const autoplan = autoplanPath ? JSON.parse(await readFile(resolve(base, autoplanPath), 'utf8')) : [];
  const guide = guidePath ? JSON.parse(await readFile(resolve(base, guidePath), 'utf8')) : null;
  if (guide && guide.scenario !== scenario.id) throw new Error('Guide scenario does not match stage');
  const core = createHost({ scenario, catalog, stages: [stage2], autoplan, guide, allowFast: testMode,
    onTranscript(transcript) {
      mkdirSync(OUT, { recursive: true });
      const stamp = new Date().toISOString().replaceAll(':', '-');
      writeFileSync(resolve(OUT, `${transcript.scenario}.${stamp}.json`), roundedJSON(transcript));
    },
  });
  const server = createServer(async (req, res) => {
    try {
      if (!['GET', 'HEAD'].includes(req.method)) { res.writeHead(405).end(); return; }
      const pathname = decodeURIComponent(new URL(req.url, 'http://localhost').pathname);
      if (pathname === '/transcript.json') {
        res.writeHead(200, { 'Content-Type': 'application/json',
          'Content-Disposition': `attachment; filename="${core.transcript.scenario}.json"` });
        res.end(req.method === 'HEAD' ? undefined : roundedJSON(core.transcript)); return;
      }
      const voice = pathname.startsWith('/Voice/');
      const manual = pathname.match(/^\/manual\/([a-z0-9_]+)\.(html|pdf)$/);
      const root = manual ? MANUALS : voice ? VOICE : WEB;
      const path = resolve(root, manual ? `${manual[1]}/manual.${manual[2]}`
        : voice ? pathname.slice('/Voice/'.length) : `.${pathname === '/' ? '/plan.html' : pathname}`);
      const rel = relative(root, path);
      if (rel.startsWith(`..${sep}`) || rel === '..' || isAbsolute(rel) || !(await stat(path)).isFile()) {
        res.writeHead(404).end(); return;
      }
      res.writeHead(200, { 'Content-Type': MIME[extname(path)] ?? 'application/octet-stream', 'Cache-Control': 'no-store' });
      res.end(req.method === 'HEAD' ? undefined : await readFile(path));
    } catch { res.writeHead(404).end(); }
  });
  const wss = new WebSocketServer({ server, path: '/ws' });
  wss.on('connection', socket => {
    socket.on('error', error => console.error('WebSocket:', error.message));
    core.attach({
      send(message) { if (socket.readyState === WebSocket.OPEN) socket.send(JSON.stringify(message)); },
      onMessage(receive) {
        socket.on('message', raw => {
          let message;
          try { message = JSON.parse(raw); } catch { return; }
          receive(message);
        });
      },
      onClose(callback) { socket.on('close', callback); },
      close() { socket.terminate(); },
    });
  });
  try {
    await new Promise((ready, reject) => { server.once('error', reject); server.listen(port, ready); });
  } catch (error) { core.close(); wss.close(); throw error; }
  return { server, core, port: server.address().port,
    close() { core.close(); wss.close(); return new Promise(done => server.close(done)); },
  };
}

async function main(args) {
  const [scenarioPath, ...options] = args;
  if (!scenarioPath || scenarioPath.startsWith('--')) throw new Error('Usage: node server.js <scenario.json> [--port 7331] [--stage2 <scenario.json>] [--autoplan <script.json>] [--guide <guide.json>]');
  const config = { scenarioPath };
  for (let i = 0; i < options.length; i++) {
    if (options[i] === '--port' && options[i + 1]) config.port = Number(options[++i]);
    else if (options[i] === '--stage2' && options[i + 1]) config.stage2Path = options[++i];
    else if (options[i] === '--autoplan' && options[i + 1]) config.autoplanPath = options[++i];
    else if (options[i] === '--guide' && options[i + 1]) config.guidePath = options[++i];
    else throw new Error(`Unknown or incomplete option: ${options[i]}`);
  }
  const host = await startHost(config);
  console.log(`LOB host listening on ${host.port}`);
  for (const signal of ['SIGINT', 'SIGTERM']) process.once(signal, () => host.close().then(() => process.exit(0)));
}
if (process.argv[1] && pathToFileURL(resolve(process.argv[1])).href === import.meta.url) {
  main(process.argv.slice(2)).catch(error => { console.error(error.message); process.exitCode = 1; });
}
