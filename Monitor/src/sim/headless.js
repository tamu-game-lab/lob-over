import { log } from '../common/log.js';

log.info('Usage: npm.cmd --prefix Monitor run headless');
log.warn('Headless simulation is a stub until WO-02.');
process.exitCode = 2;
