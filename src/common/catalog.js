// Keep synchronous filesystem loaders on Node without requesting Node modules in a browser.
const node = typeof process !== 'undefined' && Boolean(process.versions?.node);
const { readFileSync } = node ? await import('node:fs') : {};

// Resolve from this module so commands work from either Monitor or the repo root.
const DEFAULT_CATALOG_PATH = new URL('../../../Data/catalog.json', import.meta.url);

export function loadCatalog(path = DEFAULT_CATALOG_PATH) {
  if (!node) throw new Error('Use loadCatalogFromObject in the browser');
  return JSON.parse(readFileSync(path, 'utf8'));
}

// Data/catalog.json remains the source of every shared game constant.
let catalog = node ? loadCatalog() : null;

export function loadCatalogFromObject(obj) {
  catalog = structuredClone(obj);
  return structuredClone(catalog);
}

function lookup(entries, kind, key) {
  if (!Object.hasOwn(entries, key)) {
    throw new Error(`Unknown ${kind} key: "${key}"`);
  }
  return entries[key];
}

export function machine(key) {
  return lookup(catalog.machines, 'machine', key);
}

export function protection(key) {
  return lookup(catalog.protection, 'protection', key);
}

export function city(key) {
  return lookup(catalog.cities, 'city', key);
}
