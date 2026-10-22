import { cp, mkdir } from 'node:fs/promises';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { resolve } from 'node:path';
import { build as buildShowcase, dist } from '../Monitor/src/showcase/build.js';

const root = fileURLToPath(new URL('../', import.meta.url));

// WO-L6 adds Level 1 to the existing stages without changing their build inputs.
export async function build() {
  await buildShowcase();
  for (const name of ['play.html', 'img']) {
    await cp(resolve(root, 'Monitor/web', name), resolve(dist, name), { recursive: true });
  }
  await mkdir(resolve(dist, 'level1'), { recursive: true });
  for (const name of ['rules.pdf', 'rules.html']) {
    await cp(resolve(root, 'Paper/out/level1', name), resolve(dist, 'level1', name));
  }
  await cp(resolve(root, 'Voice/level1_texas'), resolve(dist, 'Voice/level1_texas'), { recursive: true });
  console.log('Level 1 included: play.html, img/, rules and Voice/level1_texas/');
}

if (process.argv[1] && pathToFileURL(resolve(process.argv[1])).href === import.meta.url) await build();
