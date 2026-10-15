import { log } from '../common/log.js';

log.info('Usage: npm.cmd --prefix Monitor run validate -- <scenario.json>');
log.warn('Scenario validation is a stub until WO-01.');
process.exitCode = 2;
