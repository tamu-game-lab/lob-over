// Docs/SCENARIO_SCHEMA.md, kit: four decimal digits, then digitSumMod10.
const CODE_DIGITS = 4;
const CHECK_DIGIT_MODULUS = 10;
const CODE_PATTERN = new RegExp(`^[0-9]{${CODE_DIGITS}}-[0-9]$`);

export function checkDigitOk(code) {
  if (typeof code !== 'string' || !CODE_PATTERN.test(code) || code.length !== CODE_DIGITS + 2) return false;
  const sum = [...code.slice(0, CODE_DIGITS)].reduce((total, digit) => total + Number(digit), 0);
  return sum % CHECK_DIGIT_MODULUS === Number(code.at(-1));
}

// Boundary convention for WO-01: [fromFt, toFt), with the job endpoint in the last zone.
export function zoneAt(zones, lengthFt, ft) {
  if (!Number.isFinite(ft) || ft < 0 || ft > lengthFt) return undefined;
  return zones.find(zone => zone && ft >= zone.fromFt
    && (ft < zone.toFt || (ft === lengthFt && zone.toFt === lengthFt)))?.id;
}

export function deepFreeze(value) {
  if (value !== null && (typeof value === 'object' || typeof value === 'function') && !Object.isFrozen(value)) {
    Object.values(value).forEach(deepFreeze);
    Object.freeze(value);
  }
  return value;
}
