import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

// Resolve from this module so commands work from either Monitor or the repo root.
const DEFAULT_CATALOG_PATH = fileURLToPath(new URL('../../../Data/catalog.json', import.meta.url));

export function loadCatalog(path = DEFAULT_CATALOG_PATH) {
  return JSON.parse(readFileSync(path, 'utf8'));
}

// Data/catalog.json remains the source of every shared game constant.
const catalog = loadCatalog();

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
