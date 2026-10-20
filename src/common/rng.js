// Mulberry32 uses one unsigned 32-bit state word. Pass state() back to makeRng
// to resume immediately after the last draw, including after JSON serialization.
export function makeRng(seed) {
  if (!Number.isInteger(seed) || seed < 0 || seed > 0xffffffff) {
    throw new RangeError('RNG seed must be an unsigned 32-bit integer');
  }
  let current = seed;

  function next() {
    current = (current + 0x6d2b79f5) >>> 0;
    let value = current;
    value = Math.imul(value ^ (value >>> 15), value | 1);
    value ^= value + Math.imul(value ^ (value >>> 7), value | 61);
    return ((value ^ (value >>> 14)) >>> 0) / 0x100000000;
  }

  // Both endpoints are inclusive so pick can address every array element.
  function int(lo, hi) {
    if (!Number.isSafeInteger(lo) || !Number.isSafeInteger(hi)
        || lo > hi || !Number.isSafeInteger(hi - lo + 1)) {
      throw new RangeError('RNG integer bounds must be ordered safe integers');
    }
    return lo + Math.floor(next() * (hi - lo + 1));
  }

  function pick(array) {
    if (!Array.isArray(array) || array.length === 0) {
      throw new RangeError('RNG pick requires a non-empty array');
    }
    return array[int(0, array.length - 1)];
  }

  return { next, int, pick, state: () => current };
}
