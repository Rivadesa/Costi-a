import assert from 'node:assert/strict';
import { euroInputToCents } from '../src/utils/money.js';
for (const [text, cents] of [['4',400], ['4,5',450], ['0.29',29], ['001.01',101], ['0',0], ['1000000.00',100000000]]) assert.equal(euroInputToCents(text), cents);
for (const text of ['', 'NaN', '-1', '1e2', '1.234', '1,000.00', '1000000.01', 'Infinity']) assert.throws(() => euroInputToCents(text));
console.log('Exact catalog money parsing: 14 cases passed');
