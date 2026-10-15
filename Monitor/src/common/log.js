import { format } from 'node:util';

const LEVELS = ['debug', 'info', 'warn', 'error'];

export function createLogger(level = 'info') {
  const threshold = LEVELS.indexOf(level);
  if (threshold === -1) {
    throw new Error(`Unknown log level: "${level}"`);
  }
  return Object.fromEntries(LEVELS.map((name, index) => [name, (...args) => {
    if (index >= threshold) {
      // Keep stdout available for machine-readable command output.
      process.stderr.write(`[${name}] ${format(...args)}\n`);
    }
  }]));
}

export const log = createLogger();
