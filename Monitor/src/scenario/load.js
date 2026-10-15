import { readFileSync } from 'node:fs';
import { loadCatalog } from '../common/catalog.js';
import { validateScenario, formatProblem } from './validate.js';
import { checkDigitOk, deepFreeze, zoneAt } from './helpers.js';

// WO-01 and Docs/SCENARIO_SCHEMA.md, job.stationEveryFt: stations every 100 feet.
const STATION_EVERY_FT = 100;

/** Synchronous load; catalog is a parsed catalog object and defaults to Data/catalog.json. */
export function loadScenario(path, { catalog = loadCatalog() } = {}) {
  const data = JSON.parse(readFileSync(path, 'utf8'));
  const result = validateScenario(data, catalog);
  if (!result.ok) {
    const error = new Error(result.problems.map(formatProblem).join('\n'));
    error.name = 'ScenarioValidationError';
    error.problems = result.problems;
    throw error;
  }

  // Copy catalog numbers so mutating the caller's catalog cannot change a loaded scenario.
  const rates = new Map(data.crews.map(crew => [crew.id, catalog.machines[crew.machine].rateFtPerDay]));
  const dailyFleetUsd = data.crews.reduce((sum, crew) => sum + catalog.machines[crew.machine].dailyUsd, 0);
  const stations = [];
  for (let ft = 0; ft <= data.job.lengthFt; ft += STATION_EVERY_FT) stations.push(ft);

  return deepFreeze({
    ...data,
    stations,
    dailyFleetUsd,
    zoneOf: ft => zoneAt(data.job.zones, data.job.lengthFt, ft),
    crewRate: crewId => rates.get(crewId),
    checkDigitOk,
  });
}
