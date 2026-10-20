// Docs/PROTOCOL.md, Transport: station 7+20 represents 720 integer feet.
const FEET_PER_STATION = 100;
const STATION_REMAINDER_DIGITS = 2;
// Docs/PROTOCOL.md, Transport: simulated day 0 starts display Day 1.
const DISPLAY_DAY_OFFSET = 1;

const usdFormatter = new Intl.NumberFormat('en-US', {
  style: 'currency',
  currency: 'USD',
  // WO-00 specifies whole-dollar display, for example $278,400.
  minimumFractionDigits: 0,
  maximumFractionDigits: 0,
});

export function fmtStation(ft) {
  if (!Number.isSafeInteger(ft) || ft < 0) {
    throw new RangeError('Station feet must be a non-negative safe integer');
  }
  const station = Math.floor(ft / FEET_PER_STATION);
  const remainder = String(ft % FEET_PER_STATION).padStart(STATION_REMAINDER_DIGITS, '0');
  return `${station}+${remainder}`;
}

export function parseStation(station) {
  if (typeof station !== 'string' || !/^\d+\+\d{2}$/.test(station)) {
    throw new Error('Station must use the format 7+20');
  }
  const [whole, remainder] = station.split('+').map(Number);
  const ft = whole * FEET_PER_STATION + remainder;
  if (!Number.isSafeInteger(ft)) {
    throw new RangeError('Station feet must be a safe integer');
  }
  return ft;
}

export function displayDay(t) {
  return Math.floor(t) + DISPLAY_DAY_OFFSET;
}

export function fmtUsd(n) {
  return usdFormatter.format(n);
}
