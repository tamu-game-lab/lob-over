import test from 'node:test';
import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
import { loadCatalog, machine, protection, city } from '../src/common/catalog.js';

test('default catalog contains the authored machines and Texas delay', () => {
  const catalog = loadCatalog();
  assert.equal(Object.keys(catalog.machines).length, 8);
  assert.equal(catalog.machines.excavator_standard.rateFtPerDay, 220);
  assert.equal(catalog.cities.texas.remobilizationDelayDays, 1);
});

test('catalog also loads from an explicit path', () => {
  const path = fileURLToPath(new URL('../../Data/catalog.json', import.meta.url));
  assert.deepEqual(loadCatalog(path), loadCatalog());
});

test('named helpers return the matching catalog entries', () => {
  const catalog = loadCatalog();
  assert.deepEqual(machine('excavator_standard'), catalog.machines.excavator_standard);
  assert.deepEqual(protection('trench_box'), catalog.protection.trench_box);
  assert.deepEqual(city('texas'), catalog.cities.texas);
});

test('all helpers reject unknown and inherited keys clearly', () => {
  for (const [kind, lookup] of [['machine', machine], ['protection', protection], ['city', city]]) {
    for (const key of ['missing', 'toString', '__proto__']) {
      assert.throws(() => lookup(key), { message: `Unknown ${kind} key: "${key}"` });
    }
  }
});
