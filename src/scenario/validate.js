import { checkDigitOk, zoneAt } from './helpers.js';

// Docs/SCENARIO_SCHEMA.md: display days and zone ids start at 1.
const FIRST_DAY = 1;
const FIRST_ZONE = 1;
// Docs/SCENARIO_SCHEMA.md: events, deviations, and the lines table.
const EVENT_TYPES = new Set(['E1', 'E2', 'P1', 'P2', 'P3', 'B1', 'A1', 'A2']);
const EVENT_TRIGGERS = new Set(['crewReaches', 'day', 'zoneSigned']);
const DEVIATION_TYPES = new Set(['F1', 'F2', 'F3', 'F4', 'F5']);
const DEVIATION_TRIGGERS = new Set([
  'firstRadioOrder', 'firstUnpermittedBoundary', 'protectStretchMisses', 'firstLend',
]);
const LINE_TRIGGERS = new Set([
  'stageStart', 'holdCommitted', 'demobCommitted', 'prepRefused', 'orderLanded',
  'orderNotDone', 'conflict', 'heldDay1', 'heldDay2', 'heldDay3', 'stageEnd',
]);
const SPEAKERS = new Set(['pm', 'radio', 'foreman_exc', 'foreman_pipe', 'foreman_back']);
const SEATS = new Set(['site', 'plan', 'rules']);
const PERMIT_HOLDERS = new Set(['pm', 'rules']);
// WO-01 also forbids score, case-insensitively, at every depth.
const FORBIDDEN_KEYS = new Set(['whenright', 'whenwrong', 'correct', 'verdict', 'score']);

const isObject = value => value !== null && typeof value === 'object' && !Array.isArray(value);
const describe = value => JSON.stringify(value) ?? 'undefined';
const knownKey = (entries, key) => typeof key === 'string' && !key.startsWith('_')
  && isObject(entries) && Object.hasOwn(entries, key);

function childPath(path, key) {
  return /^[A-Za-z_$][\w$]*$/.test(key) ? `${path}.${key}` : `${path}[${JSON.stringify(key)}]`;
}

function scan(value, source, path, problems, toValidate) {
  if (value === null || typeof value !== 'object') return;
  if (Array.isArray(value)) {
    value.forEach((item, index) => scan(item, source, `${path}[${index}]`, problems, toValidate));
    return;
  }
  for (const [key, item] of Object.entries(value)) {
    const nextPath = childPath(path, key);
    if (FORBIDDEN_KEYS.has(key.toLowerCase())) {
      problems.push({ id: 'FORBIDDEN_KEY', path: nextPath, message: `${source}: forbidden key ${describe(key)}` });
    }
    if (key === '_validate' || (key.startsWith('_') && key.endsWith('_validate'))) {
      toValidate.push({ source, path: nextPath, text: item });
    }
    // Metadata is not game data, but "anywhere" still includes its review notes and forbidden keys.
    scan(item, source, nextPath, problems, toValidate);
  }
}

/** Problems are { id, path, message }; review notes are { source, path, text }. */
export function validateScenario(scenario, catalog) {
  const problems = [];
  const toValidate = [];
  const add = (id, path, message) => problems.push({ id, path, message });
  const array = (value, path, optional = false) => {
    if (optional && value === undefined) return [];
    if (Array.isArray(value)) return value;
    add('ARRAY_REQUIRED', path, 'expected an array');
    return [];
  };
  scan(scenario, 'scenario', '$', problems, toValidate);
  scan(catalog, 'catalog', '$', problems, toValidate);
  if (!isObject(scenario)) {
    add('SCENARIO_OBJECT', '$', 'expected a scenario object');
    return { ok: false, problems, toValidate };
  }

  if (!knownKey(catalog?.cities, scenario.city)) {
    add('CITY_UNKNOWN', '$.city', `unknown catalog city ${describe(scenario.city)}`);
  }
  const crews = array(scenario.crews, '$.crews');
  const crewIds = new Set(crews.map(crew => crew?.id).filter(id => typeof id === 'string'));
  const crewOk = id => id === 'all' || crewIds.has(id);
  crews.forEach((crew, index) => {
    const path = `$.crews[${index}]`;
    if (!knownKey(catalog?.machines, crew?.machine)) {
      add('MACHINE_UNKNOWN', `${path}.machine`, `unknown catalog machine ${describe(crew?.machine)}`);
    }
    if (!Number.isFinite(crew?.startDay) || crew.startDay < FIRST_DAY) {
      add('START_DAY', `${path}.startDay`, `must be at least ${FIRST_DAY}`);
    }
  });

  const zones = array(scenario.job?.zones, '$.job.zones');
  const lengthFt = scenario.job?.lengthFt;
  let end = 0;
  let tiles = zones.length > 0 && Number.isFinite(lengthFt) && lengthFt > 0;
  zones.forEach((zone, index) => {
    if (zone?.id !== index + FIRST_ZONE) {
      add('ZONE_IDS', `$.job.zones[${index}].id`, `expected ${index + FIRST_ZONE}`);
    }
    if (!Number.isFinite(zone?.fromFt) || !Number.isFinite(zone?.toFt)
        || zone.fromFt !== end || zone.toFt <= zone.fromFt) tiles = false;
    end = zone?.toFt;
  });
  if (!tiles || end !== lengthFt) {
    add('ZONE_TILING', '$.job.zones', `must tile [0, ${describe(lengthFt)}] without gaps or overlaps`);
  }

  const plan = scenario.plan;
  if (!Number.isFinite(plan?.promisedFinishDay) || !Number.isFinite(plan?.deadlineDay)
      || plan.promisedFinishDay >= plan.deadlineDay) {
    add('PLAN_DEADLINE', '$.plan.promisedFinishDay', 'must be less than deadlineDay');
  }
  if (!Number.isFinite(plan?.budgetUsd) || plan.budgetUsd <= 0) {
    add('PLAN_BUDGET', '$.plan.budgetUsd', 'must be greater than 0');
  }

  function checkCards(section, codeKey, prefix) {
    const path = `$.${section}.cards`;
    const cards = array(scenario[section]?.cards, path);
    const zoneIds = new Set(zones.map(zone => zone?.id));
    const counts = new Map();
    const codes = new Set();
    cards.forEach((card, index) => {
      counts.set(card?.zone, (counts.get(card?.zone) ?? 0) + 1);
      const code = card?.[codeKey];
      const codePath = `${path}[${index}].${codeKey}`;
      if (codes.has(code)) add(`${prefix}_CODE_DUPLICATE`, codePath, `duplicate code ${describe(code)}`);
      codes.add(code);
      if (!checkDigitOk(code)) {
        add(`${prefix}_CHECK_DIGIT`, codePath, `invalid code ${describe(code)}; expected dddd-c with c = digit sum mod 10`);
      }
    });
    if (cards.length !== zones.length || [...counts.keys()].some(id => !zoneIds.has(id))
        || [...zoneIds].some(id => counts.get(id) !== 1)) {
      add(`${prefix}_ZONE_COVERAGE`, path, 'must contain exactly one card per zone');
    }
  }
  checkCards('permits', 'code', 'PERMIT');
  checkCards('inspections', 'number', 'INSPECTION');
  if (!PERMIT_HOLDERS.has(scenario.permits?.heldBy)) {
    add('PERMIT_HOLDER', '$.permits.heldBy', 'must be pm or rules');
  }
  const limit = scenario.permits?.openZoneLimit;
  if (!Number.isInteger(limit) || limit < FIRST_ZONE || limit > zones.length) {
    add('OPEN_ZONE_LIMIT', '$.permits.openZoneLimit', `must be an integer from ${FIRST_ZONE} to ${zones.length}`);
  }

  array(scenario.events, '$.events', true).forEach((event, index) => {
    const path = `$.events[${index}]`;
    if (!EVENT_TYPES.has(event?.type)) add('EVENT_TYPE', `${path}.type`, `unknown type ${describe(event?.type)}`);
    if (!EVENT_TRIGGERS.has(event?.trigger?.kind)) {
      add('EVENT_TRIGGER_KIND', `${path}.trigger.kind`, `unknown kind ${describe(event?.trigger?.kind)}`);
    }
    if ((event?.trigger?.kind === 'crewReaches' || Object.hasOwn(event?.trigger ?? {}, 'crew'))
        && !crewOk(event?.trigger?.crew)) {
      add('EVENT_TRIGGER_CREW', `${path}.trigger.crew`, `unknown crew ${describe(event?.trigger?.crew)}`);
    }
    if (!crewOk(event?.hits)) add('EVENT_HITS', `${path}.hits`, `unknown crew ${describe(event?.hits)}`);
    array(event?.options, `${path}.options`, true).forEach((option, optionIndex) => {
      if (!SEATS.has(option?.check?.seat)) {
        add('EVENT_CHECK_SEAT', `${path}.options[${optionIndex}].check.seat`, `unknown seat ${describe(option?.check?.seat)}`);
      }
    });
    if (event?.zone != null && event?.stationFt != null
        && zoneAt(zones, lengthFt, event.stationFt) !== event.zone) {
      add('EVENT_ZONE_STATION', `${path}.zone`, `zone ${describe(event.zone)} does not contain stationFt ${describe(event.stationFt)}`);
    }
  });
  array(scenario.deviations, '$.deviations', true).forEach((deviation, index) => {
    const path = `$.deviations[${index}]`;
    if (!DEVIATION_TYPES.has(deviation?.type)) {
      add('DEVIATION_TYPE', `${path}.type`, `unknown type ${describe(deviation?.type)}`);
    }
    if (!DEVIATION_TRIGGERS.has(deviation?.when?.kind)) {
      add('DEVIATION_WHEN_KIND', `${path}.when.kind`, `unknown kind ${describe(deviation?.when?.kind)}`);
    }
  });
  const lineIds = new Set();
  array(scenario.lines, '$.lines', true).forEach((line, index) => {
    const path = `$.lines[${index}]`;
    if (!LINE_TRIGGERS.has(line?.trigger)) {
      add('LINE_TRIGGER', `${path}.trigger`, `unknown trigger ${describe(line?.trigger)}`);
    }
    if (!SPEAKERS.has(line?.speaker)) {
      add('LINE_SPEAKER', `${path}.speaker`, `unknown speaker ${describe(line?.speaker)}`);
    }
    if (lineIds.has(line?.id)) add('LINE_ID_DUPLICATE', `${path}.id`, `duplicate id ${describe(line?.id)}`);
    lineIds.add(line?.id);
  });
  array(scenario.inbox, '$.inbox', true).forEach((item, index) => {
    if (!Number.isFinite(item?.day) || item.day < FIRST_DAY) {
      add('INBOX_DAY', `$.inbox[${index}].day`, `must be at least ${FIRST_DAY}`);
    }
  });
  return { ok: problems.length === 0, problems, toValidate };
}

export function formatProblem({ id, path, message }) {
  return `[${id}] ${path}: ${message}`.replace(/[\r\n]+/g, ' ');
}
