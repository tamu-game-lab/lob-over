import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { loadCatalog, loadCatalogFromObject, machine } from '../src/common/catalog.js';
import { loadScenario, loadScenarioFromObject } from '../src/scenario/load.js';
const read = url => JSON.parse(readFileSync(url, 'utf8'));
test('object catalog and all scenario loaders match filesystem values without owning caller objects', () => {
  const authored = loadCatalog(), catalog = loadCatalogFromObject(authored);
  assert.deepEqual(catalog, authored);
  catalog.machines.excavator_standard.rateFtPerDay = 999;
  assert.equal(machine('excavator_standard').rateFtPerDay, authored.machines.excavator_standard.rateFtPerDay);
  for (const id of ['slice_texas', 'stage1_texas', 'stage2_texas']) {
    const path = new URL(`../../Data/Scenarios/${id}.json`, import.meta.url), raw = read(path);
    const object = loadScenarioFromObject(raw, authored), file = loadScenario(path, { catalog: authored });
    assert.equal(JSON.stringify(object), JSON.stringify(file));
    assert.equal(object.crewRate('exc'), file.crewRate('exc')); assert.equal(object.zoneOf(720), file.zoneOf(720));
    assert.ok(Object.isFrozen(object.job.zones)); assert.equal(Object.isFrozen(raw.job.zones), false);
    raw.job.zones[0].toFt = 0; assert.notEqual(object.job.zones[0].toFt, 0);
  }
});
test('object scenario loader preserves validation errors and problem paths', () => {
  const raw = read(new URL('./fixtures/bad_checkdigit.json', import.meta.url));
  assert.throws(() => loadScenarioFromObject(raw, loadCatalog()), error => {
    assert.equal(error.name, 'ScenarioValidationError');
    assert.deepEqual(error.problems.map(p => p.id), ['PERMIT_CHECK_DIGIT']); return true;
  });
});
