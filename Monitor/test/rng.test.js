import test from 'node:test';
import assert from 'node:assert/strict';
import { makeRng } from '../src/common/rng.js';

test('same seed produces the same first 100 values in [0, 1)', () => {
  const a = makeRng(1);
  const b = makeRng(1);
  for (let i = 0; i < 100; i++) {
    const value = a.next();
    assert.equal(value, b.next());
    assert.ok(value >= 0 && value < 1);
  }
});

test('different seeds produce different sequences', () => {
  const a = makeRng(1);
  const b = makeRng(2);
  assert.notDeepEqual(Array.from({ length: 100 }, () => a.next()),
    Array.from({ length: 100 }, () => b.next()));
});

test('JSON state round-trip resumes the sequence after mixed draws', () => {
  const original = makeRng(0xffffffff);
  original.next();
  original.int(1, 6);
  original.pick(['a', 'b', 'c']);
  const saved = JSON.parse(JSON.stringify(original.state()));
  const resumed = makeRng(saved);
  assert.equal(resumed.state(), saved);
  for (let i = 0; i < 100; i++) {
    assert.equal(original.next(), resumed.next());
    assert.equal(original.state(), resumed.state());
  }
});

test('integer draws include both bounds and pick returns array members', () => {
  const rng = makeRng(7);
  const seen = new Set();
  const choices = ['a', 'b', 'c'];
  for (let i = 0; i < 100; i++) {
    const value = rng.int(-1, 1);
    assert.ok(Number.isInteger(value) && value >= -1 && value <= 1);
    seen.add(value);
    assert.ok(choices.includes(rng.pick(choices)));
  }
  assert.deepEqual(seen, new Set([-1, 0, 1]));
  assert.equal(rng.int(4, 4), 4);
  assert.equal(rng.pick(['only']), 'only');
  assert.throws(() => rng.pick([]), /non-empty array/);
  assert.throws(() => rng.int(2, 1), /bounds/);
});
