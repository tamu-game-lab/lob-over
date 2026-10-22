import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { once } from 'node:events';
import { createServer } from 'node:net';
import { fileURLToPath } from 'node:url';
import WebSocket from 'ws';

const root = fileURLToPath(new URL('../', import.meta.url));
export async function startRelay() {
  const reservation = createServer();
  reservation.listen(0, '127.0.0.1'); await once(reservation, 'listening');
  const port = reservation.address().port;
  await new Promise(resolve => reservation.close(resolve));
  assert.ok(![7331, 7333, 7341].includes(port));
  const args = ['dev', '--local', '--ip', '127.0.0.1', '--port', String(port),
    '--inspector-port', '0', '--show-interactive-dev-session=false'];
  const command = process.platform === 'win32' ? process.env.ComSpec : root + 'node_modules/.bin/wrangler';
  const commandArgs = process.platform === 'win32'
    ? ['/d', '/s', '/c', `""${root}node_modules/.bin/wrangler.cmd" ${args.join(' ')}"`] : args;
  const child = spawn(command, commandArgs, {
    cwd: root, windowsHide: true, windowsVerbatimArguments: process.platform === 'win32', stdio: ['pipe', 'pipe', 'pipe'],
    env: { ...process.env, WRANGLER_SEND_METRICS: 'false',
      WRANGLER_LOG_PATH: root + '.wrangler/logs', CI: 'true' },
  });
  let logs = '', exited = false;
  child.stdout.on('data', data => { logs += data; });
  child.stderr.on('data', data => { logs += data; });
  child.once('exit', () => { exited = true; });
  const base = `http://127.0.0.1:${port}`;
  async function close() {
    if (exited) return;
    const done = once(child, 'exit');
    // Kill only this test's process tree, including Wrangler's workerd child.
    if (process.platform === 'win32') {
      const stop = spawn('taskkill', ['/pid', String(child.pid), '/t', '/f'], { windowsHide: true, stdio: 'ignore' });
      await once(stop, 'exit');
    } else child.kill('SIGTERM');
    await done;
  }
  try {
    await poll(async () => {
      if (exited) throw new Error('Wrangler exited:\n' + logs);
      try { return (await fetch(base + '/rooms', { signal: AbortSignal.timeout(1000) })).ok; }
      catch { return false; }
    }, 30_000);
  } catch (error) { await close(); throw new Error(error.message + '\n' + logs); }
  return { base, ws: base.replace('http:', 'ws:'), close, logs: () => logs };
}

export async function poll(check, timeout = 5000) {
  const deadline = Date.now() + timeout;
  do {
    const result = await check();
    if (result) return result;
    await new Promise(resolve => setTimeout(resolve, 25));
  } while (Date.now() < deadline);
  throw new Error('Timed out waiting for test condition');
}

export async function connect(url, t) {
  const socket = new WebSocket(url), messages = [];
  let closed;
  socket.on('message', raw => messages.push(JSON.parse(raw)));
  socket.on('close', (code, reason) => { closed = { code, reason: String(reason) }; });
  // Each test owns every socket and closes it even on assertion failure.
  t.after(() => socket.terminate());
  await once(socket, 'open');
  return { socket, messages, send: message => socket.send(JSON.stringify(message)),
    next: predicate => poll(() => {
      const index = messages.findIndex(predicate);
      return index < 0 ? false : messages.splice(index, 1)[0];
    }),
    closed: timeout => poll(() => closed, timeout),
  };
}

export async function rejected(url, status) {
  const socket = new WebSocket(url);
  socket.on('error', () => {});
  const [, response] = await once(socket, 'unexpected-response');
  response.resume(); socket.terminate();
  assert.equal(response.statusCode, status);
}
