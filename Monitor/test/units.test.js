import test from 'node:test';
import assert from 'node:assert/strict';
import { fmtStation, parseStation, displayDay, fmtUsd } from '../src/common/units.js';

test('station examples format and parse in integer feet', () => {
  for (const [ft, station] of [[0, '0+00'], [5, '0+05'], [720, '7+20'], [1500, '15+00']]) {
    assert.equal(fmtStation(ft), station);
    assert.equal(parseStation(station), ft);
  }
});

test('display days advance only at integer day boundaries', () => {
  assert.equal(displayDay(0), 1);
  assert.equal(displayDay(0.999), 1);
  assert.equal(displayDay(1), 2);
  assert.equal(displayDay(2.5), 3);
});

test('USD example uses dollars and comma grouping', () => {
  assert.equal(fmtUsd(278400), '$278,400');
  assert.equal(fmtUsd(0), '$0');
});
