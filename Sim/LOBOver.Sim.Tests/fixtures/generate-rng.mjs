import { writeFileSync } from 'node:fs';
import { makeRng } from '../../../Monitor/src/common/rng.js';
const rng = makeRng(20261020);
const values = Array.from({ length: 100 }, () => rng.next());
writeFileSync(new URL('./rng_seed_20261020.json', import.meta.url), JSON.stringify(values, null, 2) + '\n');
