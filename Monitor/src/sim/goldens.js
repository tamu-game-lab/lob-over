import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';
import { loadCatalog } from '../common/catalog.js';
import { CREW_IDS } from './common.js';
import { createSim } from './sim.js';

// Data/goldens/no_events.json _doc: finish/idle 0.1 day, cost $500, peak 10 ft.
const TOLERANCE = { days: 0.1, cost: 500, feet: 10 };
export function runGoldens(goldens, catalog) {
  return Object.entries(goldens.cases).map(([name, entry]) => {
    const scenario = {
      id: name, seed: 0, capFt: entry.input.capFt,
      job: { lengthFt: goldens.lengthFt, stationEveryFt: 100, zones: [{ id: 1, fromFt: 0, toFt: goldens.lengthFt }] },
      crews: CREW_IDS.map(id => ({ id, name: id, machine: entry.input.machines[id], startDay: entry.input.startDay[id] })),
      plan: {}, permits: { cards: [] }, inspections: { required: false, cards: [] },
      rules: { overheadRate: catalog.money.overheadRate }, events: [], deviations: [], inbox: [], lines: [],
    };
    const sim = createSim(scenario, catalog, { pure: true });
    // The oracle uses a 300-day guard; this is a harness limit, not a game rule.
    while (!sim.done && sim.t < 300) sim.tick(goldens.dt);
    const actual = sim.result, expected = entry.expected;
    const deltas = actual ? { finishDay: actual.finishDay - expected.finishDay,
      idleDays: Object.fromEntries(CREW_IDS.map(id => [id, actual.idleDays[id] - expected.idleDays[id]])),
      crewCostUsd: actual.crewCostUsd - expected.crewCostUsd, peakOpenTrenchFt: actual.peakOpenTrenchFt - expected.peakOpenTrenchFt } : null;
    const pass = !!deltas && Math.abs(deltas.finishDay) <= TOLERANCE.days
      && CREW_IDS.every(id => Math.abs(deltas.idleDays[id]) <= TOLERANCE.days)
      && Math.abs(deltas.crewCostUsd) <= TOLERANCE.cost && Math.abs(deltas.peakOpenTrenchFt) <= TOLERANCE.feet;
    return { name, pass, deltas };
  });
}

if (process.argv[1] && pathToFileURL(resolve(process.argv[1])).href === import.meta.url) {
  try {
    const results = runGoldens(JSON.parse(readFileSync(process.argv[2], 'utf8')), loadCatalog());
    for (const { name, pass, deltas } of results) console.log(`${pass ? 'PASS' : 'FAIL'} ${name} deltas=${JSON.stringify(deltas,
      (key, value) => typeof value === 'number' ? Number(value.toFixed(3)) : value)}`);
    process.exitCode = results.every(result => result.pass) ? 0 : 1;
  } catch (error) { console.error(error.message); process.exitCode = 1; }
}
