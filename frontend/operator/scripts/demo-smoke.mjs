import assert from 'node:assert/strict';

const storage = new Map();
globalThis.localStorage = {
  getItem(key) { return storage.has(key) ? storage.get(key) : null; },
  setItem(key, value) { storage.set(key, String(value)); },
  removeItem(key) { storage.delete(key); },
  clear() { storage.clear(); },
};

const { demoRequest, resetDemo } = await import('../src/api/demo.js');
resetDemo();

const configuration = await demoRequest('/configuration');
assert.equal(configuration.data.tables.length, 8);
assert.ok(configuration.data.stations.length >= 5);

const initialBoard = await demoRequest('/service-board');
assert.equal(initialBoard.data.length, 3);
assert.ok(initialBoard.data.some((row) => row.critical_restrictions.length > 0));

const service = await demoRequest('/services/demo-service-1');
const course = service.courses.find((candidate) => ['fired', 'preparing'].includes(candidate.status));
assert.ok(course, 'Mesa 1 should have an active kitchen course');

for (const item of course.items) {
  if (item.status === 'fired') {
    await demoRequest(`/services/${service.id}/courses/${course.id}/items/${item.id}/start`, { method: 'POST' });
    await demoRequest(`/services/${service.id}/courses/${course.id}/items/${item.id}/ready`, { method: 'POST' });
  } else if (item.status === 'preparing') {
    await demoRequest(`/services/${service.id}/courses/${course.id}/items/${item.id}/ready`, { method: 'POST' });
  }
}

await demoRequest(`/services/${service.id}/courses/${course.id}/ready`, { method: 'POST' });
const ready = await demoRequest(`/services/${service.id}`);
assert.equal(ready.courses.find((candidate) => candidate.id === course.id).status, 'ready');

await demoRequest(`/services/${service.id}/courses/${course.id}/serve`, { method: 'POST' });
const served = await demoRequest(`/services/${service.id}`);
assert.equal(served.courses.find((candidate) => candidate.id === course.id).status, 'served');

const next = await demoRequest(`/services/${service.id}/courses/fire-next`, { method: 'POST' });
assert.equal(next.status, 'fired');

const kds = await demoRequest(`/kds/stations/${next.items[0].station_id}`);
assert.ok(kds.data.some((ticket) => ticket.service_id === service.id && ticket.course_id === next.id));

console.log('Demo workflow smoke test passed');
