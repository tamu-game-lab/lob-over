import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { loadCatalog } from '../src/common/catalog.js';
import { loadScenario } from '../src/scenario/load.js';
import { validateScenario } from '../src/scenario/validate.js';

const root = fileURLToPath(new URL('../../', import.meta.url));
const cli = fileURLToPath(new URL('../src/scenario/validate.js', import.meta.url));
const stagePath = id => new URL(`../../Data/Scenarios/${id}.json`, import.meta.url);
const fixturePath = name => new URL(`./fixtures/${name}.json`, import.meta.url);
const read = path => JSON.parse(readFileSync(path, 'utf8'));
const catalog = loadCatalog();
const stage = () => read(stagePath('stage1_texas'));
const ids = result => result.problems.map(problem => problem.id);
const run = (args, cwd = root) => {
  const result = spawnSync(process.execPath, [cli, ...args], { cwd, encoding: 'utf8' });
  assert.ifError(result.error);
  assert.equal(result.stderr, '');
  return result;
};

for (const id of ['stage1_texas', 'stage2_texas']) {
  test(`${id} validates, reports separate review notes, and loads frozen derived values`, () => {
    const raw = read(stagePath(id));
    const result = validateScenario(raw, catalog);
    assert.equal(result.ok, true);
    assert.deepEqual(result.problems, []);
    assert.ok(result.toValidate.filter(note => note.source === 'scenario').length >= 2);
    assert.ok(result.toValidate.filter(note => note.source === 'catalog').length >= 6);
    for (const note of result.toValidate) {
      assert.ok(note.path.startsWith('$.'));
      assert.equal(typeof note.text, 'string');
    }

    const loaded = loadScenario(stagePath(id), { catalog });
    for (const [key, value] of Object.entries(raw)) assert.deepEqual(loaded[key], value);
    assert.equal(loaded.dailyFleetUsd, 4800);
    assert.deepEqual(['exc', 'pipe', 'back'].map(loaded.crewRate), [220, 180, 250]);
    assert.equal(loaded.crewRate('unknown'), undefined);
    assert.deepEqual(loaded.stations, Array.from({ length: 61 }, (_, index) => index * 100));
    assert.ok(Object.isFrozen(loaded));
    assert.ok(Object.isFrozen(loaded.job.zones[0]));
    assert.ok(Object.isFrozen(loaded.events[0].options[0].check));
    assert.ok(Object.isFrozen(loaded.stations));
    assert.throws(() => { loaded.job.zones[0].toFt = 1; }, TypeError);
    assert.throws(() => loaded.stations.push(6100), TypeError);
    assert.equal(loaded.zoneOf(720), 1);
  });

  test(`${id} CLI exits 0 with separate scenario/catalog lists and final OK`, () => {
    const result = run([`Data/Scenarios/${id}.json`]);
    assert.equal(result.status, 0);
    assert.match(result.stdout, /^to validate \([2-9]\):/);
    assert.match(result.stdout, /\ncatalog to validate \([6-9]\):/);
    assert.match(result.stdout, new RegExp(`OK ${id}\\r?\\n$`));
  });
}

test('zone lookup uses half-open boundaries and includes the job endpoint', () => {
  const loaded = loadScenario(stagePath('stage1_texas'));
  for (const [ft, zone] of [[0, 1], [1499, 1], [1500, 2], [3000, 3], [4500, 4], [6000, 4]]) {
    assert.equal(loaded.zoneOf(ft), zone);
  }
  for (const ft of [-1, 6001, NaN, Infinity, '720', null]) assert.equal(loaded.zoneOf(ft), undefined);
});

test('derived fleet and rates use the supplied catalog without retaining mutable references', () => {
  const custom = structuredClone(catalog);
  custom.machines.excavator_standard.dailyUsd = 123;
  custom.machines.excavator_standard.rateFtPerDay = 456;
  const loaded = loadScenario(stagePath('stage1_texas'), { catalog: custom });
  assert.equal(loaded.dailyFleetUsd, 3723);
  assert.equal(loaded.crewRate('exc'), 456);
  custom.machines.excavator_standard.rateFtPerDay = 999;
  assert.equal(loaded.crewRate('exc'), 456);
  assert.equal(Object.isFrozen(custom), false);
});

test('check digits accept leading zeroes and reject wrong digits and malformed codes', () => {
  const { checkDigitOk } = loadScenario(stagePath('stage1_texas'));
  for (const code of ['4821-5', '0000-0', '0001-1', '9999-6']) assert.equal(checkDigitOk(code), true);
  for (const code of ['4821-4', '4821', '4821-55', '821-1', '04821-5', 'abcd-0', '4821-5\n', ' 4821-5', 48215, null]) {
    assert.equal(checkDigitOk(code), false, String(code));
  }
});

test('combined fixture catches every requested rule with stable ids and paths', () => {
  const result = validateScenario(read(fixturePath('broken_rules')), catalog);
  assert.equal(result.ok, false);
  const expected = [
    'CITY_UNKNOWN', 'MACHINE_UNKNOWN', 'START_DAY', 'ZONE_IDS', 'ZONE_TILING',
    'PLAN_DEADLINE', 'PLAN_BUDGET', 'PERMIT_ZONE_COVERAGE', 'PERMIT_CODE_DUPLICATE',
    'PERMIT_CHECK_DIGIT', 'INSPECTION_ZONE_COVERAGE', 'INSPECTION_CODE_DUPLICATE',
    'INSPECTION_CHECK_DIGIT', 'PERMIT_HOLDER', 'OPEN_ZONE_LIMIT', 'EVENT_TYPE',
    'EVENT_TRIGGER_KIND', 'EVENT_TRIGGER_CREW', 'EVENT_HITS', 'EVENT_CHECK_SEAT',
    'EVENT_ZONE_STATION', 'DEVIATION_TYPE', 'DEVIATION_WHEN_KIND', 'LINE_TRIGGER',
    'LINE_SPEAKER', 'LINE_ID_DUPLICATE', 'INBOX_DAY', 'FORBIDDEN_KEY',
  ];
  assert.deepEqual(new Set(ids(result)), new Set(expected));
  for (const problem of result.problems) {
    assert.ok(problem.path.startsWith('$.'));
    assert.ok(problem.message.length > 0);
  }
  assert.equal(result.problems.filter(problem => problem.id === 'FORBIDDEN_KEY').length, 5);
});

test('bad-check-digit fixture has only that problem and the loader rejects it', () => {
  const path = fixturePath('bad_checkdigit');
  assert.deepEqual(ids(validateScenario(read(path), catalog)), ['PERMIT_CHECK_DIGIT']);
  assert.throws(() => loadScenario(path, { catalog }), error => {
    assert.equal(error.name, 'ScenarioValidationError');
    assert.match(error.message, /4821-4/);
    assert.deepEqual(error.problems.map(problem => problem.id), ['PERMIT_CHECK_DIGIT']);
    return true;
  });
});

test('zones reject gaps, overlaps, missing endpoints, empty spans, and wrong ids separately', () => {
  const cases = [
    [data => { data.job.zones[1].fromFt += 1; }, 'ZONE_TILING'],
    [data => { data.job.zones[1].fromFt -= 1; }, 'ZONE_TILING'],
    [data => { data.job.zones[0].fromFt = 1; }, 'ZONE_TILING'],
    [data => { data.job.zones[3].toFt -= 1; }, 'ZONE_TILING'],
    [data => { data.job.zones[0].toFt = 0; }, 'ZONE_TILING'],
    [data => { data.job.zones[0].id = 0; }, 'ZONE_IDS'],
  ];
  for (const [change, id] of cases) {
    const data = stage();
    change(data);
    assert.ok(ids(validateScenario(data, catalog)).includes(id), id);
  }
});

test('both card sets reject missing, duplicate, extra, unknown-zone and malformed codes', () => {
  for (const [section, field, prefix] of [['permits', 'code', 'PERMIT'], ['inspections', 'number', 'INSPECTION']]) {
    const cases = [
      [cards => cards.pop(), 'ZONE_COVERAGE'],
      [cards => { cards[0].zone = 2; }, 'ZONE_COVERAGE'],
      [cards => cards.push({ ...cards[0] }), 'ZONE_COVERAGE'],
      [cards => { cards[0].zone = 99; }, 'ZONE_COVERAGE'],
      [cards => { cards[0][field] = cards[1][field]; }, 'CODE_DUPLICATE'],
      [cards => { cards[0][field] = '123-6'; }, 'CHECK_DIGIT'],
    ];
    for (const [change, suffix] of cases) {
      const data = stage();
      change(data[section].cards);
      assert.ok(ids(validateScenario(data, catalog)).includes(`${prefix}_${suffix}`));
    }
  }
});

test('catalog lookup rejects inherited and metadata keys', () => {
  for (const key of ['toString', '__proto__', '_validate']) {
    const data = stage();
    data.city = key;
    data.crews[0].machine = key;
    const result = validateScenario(data, catalog);
    assert.ok(ids(result).includes('CITY_UNKNOWN'));
    assert.ok(ids(result).includes('MACHINE_UNKNOWN'));
  }
});

test('numeric constraints do not coerce strings or accept nulls, infinities, or limit fractions', () => {
  for (const bad of [null, '1', NaN, Infinity, -1, 0]) {
    const data = stage();
    data.crews[0].startDay = bad;
    data.inbox[0].day = bad;
    data.plan.budgetUsd = bad;
    data.permits.openZoneLimit = bad;
    const resultIds = ids(validateScenario(data, catalog));
    for (const id of ['START_DAY', 'INBOX_DAY', 'PLAN_BUDGET', 'OPEN_ZONE_LIMIT']) assert.ok(resultIds.includes(id));
  }
  const data = stage();
  data.permits.openZoneLimit = 1.5;
  assert.ok(ids(validateScenario(data, catalog)).includes('OPEN_ZONE_LIMIT'));
});

test('schema enum lists, crew all, and absent/null event locations are accepted', () => {
  const data = stage();
  data.events = ['E1', 'E2', 'P1', 'P2', 'P3', 'B1', 'A1', 'A2'].map(type => ({
    type, trigger: { kind: 'crewReaches', crew: 'all', ft: 100 }, hits: 'all',
    options: ['site', 'plan', 'rules'].map(seat => ({ check: { seat } })),
  }));
  data.events.push({ type: 'A2', trigger: { kind: 'day', day: 1 }, hits: 'all', zone: null, stationFt: null });
  data.events.push({ type: 'B1', trigger: { kind: 'zoneSigned', zone: 1 }, hits: 'back' });
  data.deviations = ['F1', 'F2', 'F3', 'F4', 'F5'].map(type => ({ type, when: { kind: 'firstLend' } }));
  data.lines.push({ id: 'exc_line', speaker: 'foreman_exc', trigger: 'stageStart' });
  assert.deepEqual(validateScenario(data, catalog).problems, []);
});

test('event locations share loader boundary semantics and reject out-of-range stations', () => {
  const data = stage();
  for (const [ft, zone] of [[0, 1], [1500, 2], [6000, 4]]) {
    Object.assign(data.events[0], { stationFt: ft, zone });
    assert.equal(validateScenario(data, catalog).ok, true);
    data.events[0].zone = 99;
    assert.ok(ids(validateScenario(data, catalog)).includes('EVENT_ZONE_STATION'));
  }
  for (const ft of [-1, 6001, '720']) {
    Object.assign(data.events[0], { stationFt: ft, zone: 1 });
    assert.ok(ids(validateScenario(data, catalog)).includes('EVENT_ZONE_STATION'));
  }
  delete data.events[0].trigger.crew;
  assert.ok(ids(validateScenario(data, catalog)).includes('EVENT_TRIGGER_CREW'));
});

test('metadata stays out of domain validation; review notes retain source, JSON path and text', () => {
  const data = stage();
  data._draft = { events: [{ type: 'not_a_type' }], nested: [{ _validate: 'Review this.' }] };
  data['odd.key'] = { _detail_validate: 'Another review.' };
  data._validateLater = 'Not a review marker.';
  const before = JSON.stringify({ data, catalog });
  const result = validateScenario(data, catalog);
  assert.equal(result.ok, true);
  assert.ok(result.toValidate.some(note => note.source === 'scenario'
    && note.path === '$._draft.nested[0]._validate' && note.text === 'Review this.'));
  assert.ok(result.toValidate.some(note => note.path === '$["odd.key"]._detail_validate' && note.text === 'Another review.'));
  assert.ok(!result.toValidate.some(note => note.path.endsWith('_validateLater')));
  assert.deepEqual(validateScenario(data, catalog), result);
  assert.equal(JSON.stringify({ data, catalog }), before);
});

test('forbidden keys are found in catalog metadata too', () => {
  const custom = structuredClone(catalog);
  custom._draft = { Verdict: 'forbidden' };
  const result = validateScenario(stage(), custom);
  assert.deepEqual(ids(result), ['FORBIDDEN_KEY']);
  assert.equal(result.problems[0].path, '$._draft.Verdict');
  assert.match(result.problems[0].message, /catalog/);
});

test('malformed structures report problems instead of throwing', () => {
  for (const data of [null, [], 'stage', {}, { job: { zones: [null] }, crews: [null], events: [null] }]) {
    assert.equal(validateScenario(data, catalog).ok, false);
  }
  const data = stage();
  data.events = {};
  assert.ok(ids(validateScenario(data, catalog)).includes('ARRAY_REQUIRED'));
});

test('CLI reports problems first, notes second, FAIL last, and exits 1', () => {
  const result = run(['Monitor/test/fixtures/bad_checkdigit.json']);
  assert.equal(result.status, 1);
  assert.match(result.stdout, /^\[PERMIT_CHECK_DIGIT\] .*4821-4/);
  assert.ok(result.stdout.indexOf('to validate (') > result.stdout.indexOf('[PERMIT_CHECK_DIGIT]'));
  assert.match(result.stdout, /FAIL bad_checkdigit\r?\n$/);
  const multiple = run(['Monitor/test/fixtures/broken_rules.json']);
  assert.equal(multiple.status, 1);
  assert.equal(multiple.stdout.split(/\r?\n/).filter(line => line.startsWith('[')).length,
    validateScenario(read(fixturePath('broken_rules')), catalog).problems.length);
});

test('CLI returns usage 2 and file/JSON errors 1 without stack traces', () => {
  for (const args of [[], ['--help'], ['one.json', 'two.json']]) {
    const result = run(args);
    assert.equal(result.status, 2);
    assert.match(result.stdout, /^Usage:/);
  }
  for (const [path, id] of [['Monitor/test/fixtures/bad_json.json', 'JSON_PARSE'], ['Monitor/test/fixtures/missing.json', 'FILE_READ']]) {
    const result = run([path]);
    assert.equal(result.status, 1);
    assert.ok(result.stdout.startsWith(`[${id}]`));
    assert.match(result.stdout, /FAIL /);
  }
});

test('CLI resolves the catalog from Monitor and importing it has no CLI side effects', () => {
  assert.equal(run(['../Data/Scenarios/stage1_texas.json'], new URL('../', import.meta.url)).status, 0);
  const result = spawnSync(process.execPath, ['--input-type=module', '-e', 'await import("./Monitor/src/scenario/validate.js")'], {
    cwd: root, encoding: 'utf8',
  });
  assert.ifError(result.error);
  assert.equal(result.status, 0);
  assert.equal(result.stdout, '');
  assert.equal(result.stderr, '');
});
