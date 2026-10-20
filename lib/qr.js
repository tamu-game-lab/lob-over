// QR Model 2, version 6-L, byte mode, mask 0. No service or external image request.
// Version 6-L holds 134 UTF-8 bytes: two blocks of 68 data + 18 parity bytes.
const SIZE = 41, DATA = 136, PARITY = 18;
function multiply(x, y) {
  let value = 0;
  for (let i = 7; i >= 0; i--) {
    value = (value << 1) ^ ((value >>> 7) * 0x11d);
    value ^= ((y >>> i) & 1) * x;
  }
  return value;
}
function remainder(data) {
  let polynomial = [1], root = 1;
  for (let i = 0; i < PARITY; i++) {
    const next = Array(polynomial.length + 1).fill(0);
    polynomial.forEach((v, j) => { next[j] ^= v; next[j + 1] ^= multiply(v, root); });
    polynomial = next; root = multiply(root, 2);
  }
  const result = Array(PARITY).fill(0);
  for (const value of data) {
    const factor = value ^ result.shift(); result.push(0);
    result.forEach((_, j) => { result[j] ^= multiply(polynomial[j + 1], factor); });
  }
  return result;
}
export function qrMatrix(text) {
  const bytes = new TextEncoder().encode(text);
  if (bytes.length > DATA - 2) throw new Error('Room URL is too long for the QR image');
  const bits = [], append = (value, count) => { for (let i = count - 1; i >= 0; i--) bits.push((value >>> i) & 1); };
  append(4, 4); append(bytes.length, 8); for (const value of bytes) append(value, 8);
  append(0, 4); while (bits.length % 8) bits.push(0);
  const data = [];
  for (let i = 0; i < bits.length; i += 8) data.push(bits.slice(i, i + 8).reduce((a, b) => a * 2 + b, 0));
  for (let i = 0; data.length < DATA; i++) data.push(i % 2 ? 0x11 : 0xec);
  const blocks = [data.slice(0, 68), data.slice(68)], parity = blocks.map(remainder), encoded = [];
  for (let i = 0; i < 68; i++) for (const block of blocks) encoded.push(block[i]);
  for (let i = 0; i < PARITY; i++) for (const block of parity) encoded.push(block[i]);
  const matrix = Array.from({ length: SIZE }, () => Array(SIZE).fill(false));
  const reserved = matrix.map(row => row.slice());
  const put = (x, y, value) => { matrix[y][x] = Boolean(value); reserved[y][x] = true; };
  for (const [cx, cy] of [[3, 3], [SIZE - 4, 3], [3, SIZE - 4]]) {
    for (let dy = -4; dy <= 4; dy++) for (let dx = -4; dx <= 4; dx++) {
      const x = cx + dx, y = cy + dy, distance = Math.max(Math.abs(dx), Math.abs(dy));
      if (x >= 0 && y >= 0 && x < SIZE && y < SIZE) put(x, y, distance !== 2 && distance !== 4);
    }
  }
  for (let i = 8; i < SIZE - 8; i++) { put(6, i, i % 2 === 0); put(i, 6, i % 2 === 0); }
  for (let dy = -2; dy <= 2; dy++) for (let dx = -2; dx <= 2; dx++) put(34 + dx, 34 + dy, Math.max(Math.abs(dx), Math.abs(dy)) !== 1);
  // BCH format bits for L / mask 0, including the standard format mask.
  const format = 0x77c4, bit = i => (format >>> i) & 1;
  for (let i = 0; i <= 5; i++) put(8, i, bit(i));
  put(8, 7, bit(6)); put(8, 8, bit(7)); put(7, 8, bit(8));
  for (let i = 9; i < 15; i++) put(14 - i, 8, bit(i));
  for (let i = 0; i < 8; i++) put(SIZE - 1 - i, 8, bit(i));
  for (let i = 8; i < 15; i++) put(8, SIZE - 15 + i, bit(i));
  put(8, SIZE - 8, true);
  let index = 0, upward = true;
  for (let right = SIZE - 1; right > 0; right -= 2) {
    if (right === 6) right--;
    for (let vertical = 0; vertical < SIZE; vertical++) for (let offset = 0; offset < 2; offset++) {
      const x = right - offset, y = upward ? SIZE - 1 - vertical : vertical;
      if (reserved[y][x]) continue;
      const value = index < encoded.length * 8 ? (encoded[index >>> 3] >>> (7 - index % 8)) & 1 : 0;
      matrix[y][x] = Boolean(value ^ ((x + y) % 2 === 0)); index++;
    }
    upward = !upward;
  }
  return matrix;
}
export function drawQR(canvas, text) {
  const matrix = qrMatrix(text), quiet = 4, scale = 5;
  canvas.width = canvas.height = (SIZE + quiet * 2) * scale;
  const context = canvas.getContext('2d');
  context.fillStyle = '#fff'; context.fillRect(0, 0, canvas.width, canvas.height);
  context.fillStyle = '#000';
  matrix.forEach((row, y) => row.forEach((dark, x) => { if (dark) context.fillRect((x + quiet) * scale, (y + quiet) * scale, scale, scale); }));
}
