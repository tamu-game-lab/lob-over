import { cp, mkdir, readFile, readdir, rm, writeFile } from 'node:fs/promises';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { resolve, relative, sep, extname } from 'node:path';

const root = fileURLToPath(new URL('../../../', import.meta.url));
export const dist = resolve(root, 'Showcase/dist');
export const scripts = ['slice_autoplan', 'slice_play', 'slice_pull_play', 'stage1_pm_play', 'stage2_pull_play'];
export async function build() {
  // Only the generated directory is replaced; authored inputs stay in place.
  if (relative(root, dist) !== ['Showcase', 'dist'].join(sep)) throw new Error('Unexpected build directory');
  await rm(dist, { recursive: true, force: true });
  await mkdir(dist, { recursive: true });
  for (const name of ['index.html', 'host.html', 'plan.html', 'site.html', 'howto.html', 'lib']) {
    await cp(resolve(root, 'Monitor/web', name), resolve(dist, name), { recursive: true });
  }
  for (const dir of ['sim', 'scenario', 'common', 'host']) {
    await cp(resolve(root, 'Monitor/src', dir), resolve(dist, 'src', dir), { recursive: true,
      filter: path => !/\.test\.js$/.test(path) && !['server.js', 'headless.js', 'goldens.js', 'log.js'].some(name => path.endsWith(sep + name)),
    });
  }
  // validate.js is outside WO-S1's editable scope. The generated browser copy keeps
  // its pure validation functions verbatim and omits only imports used by its CLI.
  const validator = await readFile(resolve(root, 'Monitor/src/scenario/validate.js'), 'utf8');
  const marker = '\nfunction main(args) {';
  if (!validator.includes(marker)) throw new Error('Validator CLI boundary changed');
  const browserValidator = validator.slice(0, validator.indexOf(marker))
    .replace(/^import .* from 'node:[^']+';\r?\n/gm, '')
    .replace(/^import \{ loadCatalog \} from '[^']+';\r?\n/m, '');
  await writeFile(resolve(dist, 'src/scenario/validate.js'), browserValidator);
  await mkdir(resolve(dist, 'Data/scripts'), { recursive: true });
  await cp(resolve(root, 'Data/catalog.json'), resolve(dist, 'Data/catalog.json'));
  await cp(resolve(root, 'Data/Scenarios'), resolve(dist, 'Data/Scenarios'), { recursive: true,
    filter: path => !extname(path) || extname(path) === '.json',
  });
  for (const name of scripts) await cp(resolve(root, 'Monitor/test/scripts', name + '.json'), resolve(dist, 'Data/scripts', name + '.json'));
  await mkdir(resolve(dist, 'manual'), { recursive: true });
  for (const stage of ['slice_texas', 'stage1_texas', 'stage2_texas']) {
    try { await cp(resolve(root, 'Paper/out', stage, 'manual.pdf'), resolve(dist, 'manual', stage + '.pdf')); }
    catch (error) { if (error.code !== 'ENOENT') throw error; console.log(`Manual absent: ${stage}`); }
  }
  async function copyVoice(dir, target) {
    let entries;
    try { entries = await readdir(dir, { withFileTypes: true }); } catch (error) { if (error.code === 'ENOENT') return; throw error; }
    for (const entry of entries) {
      if (entry.isDirectory()) await copyVoice(resolve(dir, entry.name), resolve(target, entry.name));
      else if (entry.name.endsWith('.wav')) { await mkdir(target, { recursive: true }); await cp(resolve(dir, entry.name), resolve(target, entry.name)); }
    }
  }
  await copyVoice(resolve(root, 'Voice'), resolve(dist, 'Voice'));
  await writeFile(resolve(dist, '.nojekyll'), '');
  console.log('Showcase built: ' + dist);
}
if (process.argv[1] && pathToFileURL(resolve(process.argv[1])).href === import.meta.url) await build();
