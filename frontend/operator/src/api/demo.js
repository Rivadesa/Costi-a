const DEMO_KEY = 'hospitality.demo.state.v1';

const nowMinus = (minutes) => new Date(Date.now() - minutes * 60_000).toISOString();
const id = (prefix) => `${prefix}-${crypto.randomUUID()}`;

const menuTemplates = {
  'menu-ret',: null
};

const menus = [
  { id: 'menu-experience', name: 'Menú Experiencia', price_cents: 15000 },
  { id: 'menu-season', name: 'Menú Temporada', price_cents: 12500 },
];

const courseTemplates = {
  'menu-experience': [
    { name: 'Aperitivos', items: [{ name: 'Aperitivo', station_id: 'cold', per_guest: true }] },
    { name: 'Entrante frío', items: [{ name: 'Entrante frío', station_id: 'cold', per_guest: true }] },
    { name: 'Entrante caliente', items: [{ name: 'Entrante caliente', station_id: 'hot', per_guest: true }] },
    { name: 'Pescado', items: [{ name: 'Pescado', station_id: 'fish', per_guest: true }, { name: 'Guarnición', station_id: 'pass', per_guest: false, quantity: 1 }] },
    { name: 'Carne', items: [{ name: 'Carne', station_id: 'meat', per_guest: true }, { name: 'Guarnición', station_id: 'pass', per_guest: false, quantity: 1 }] },
    { name: 'Prepostre', items: [{ name: 'Prepostre', station_id: 'pastry', per_guest: true }] },
    { name: 'Postre', items: [{ name: 'Postre', station_id: 'pastry', per_guest: true }] },
  ],
  'menu-season': [
    { name: 'Aperitivo', items: [{ name: 'Aperitivo de temporada', station_id: 'cold', per_guest: true }] },
    { name: 'Pescado', items: [{ name: 'Pescado de lonja', station_id: 'fish', per_guest: true }] },
    { name: 'Carne', items: [{ name: 'Carne', station_id: 'meat', per_guest: true }] },
    { name: 'Postre', items: [{ name: 'Postre', station_id: 'pastry', per_guest: true }] },
  ],
};

const configuration = {
  tables: Array.from({ length: 8 }, (_, index) => ({
    id: `table-${index + 1}`,
    name: `Mesa ${index + 1}`,
    area: index < 6 ? 'Sala principal' : 'Reservado',
    capacity: index === 7 ? 8 : 6,
  })),
  menus,
  stations: [
    { id: 'cold', name: 'Fríos' },
    { id: 'hot', name: 'Calientes' },
    { id: 'fish', name: 'Pescados' },
    { id: 'meat', name: 'Carnes' },
    { id: 'pastry', name: 'Postres' },
    { id: 'pass', name: 'Pase' },
  ],
};

function instantiateCourses(menuId, pax) {
  return (courseTemplates[menuId] || []).map((template, index) => ({
    id: id('course'),
    sequence: index + 1,
    name: template.name,
    status: 'pending',
    fired_at: null,
    served_at: null,
    items: template.items.flatMap((item) => {
      if (item.per_guest) {
        return Array.from({ length: pax }, (_, guestIndex) => ({
          id: id('item'),
          station_id: item.station_id,
          name: item.name,
          quantity: 1,
          guest_position: guestIndex + 1,
          status: 'pending',
          modification_reason: null,
        }));
      }
      return [{
        id: id('item'),
        station_id: item.station_id,
        name: item.name,
        quantity: item.quantity || 1,
        guest_position: null,
        status: 'pending',
        modification_reason: null,
      }];
    }),
  }));
}

function guests(pax) {
  return Array.from({ length: pax }, (_, index) => ({
    id: id('guest'),
    position: index + 1,
    name: null,
    restrictions: [],
  }));
}

function seededService({ id: serviceId, tableId, pax, menuId, status, courseIndex, courseStatus, minutes, critical = false }) {
  const menu = menus.find((candidate) => candidate.id === menuId);
  const courses = instantiateCourses(menuId, pax);
  for (let i = 0; i < courseIndex; i += 1) {
    courses[i].status = 'served';
    courses[i].fired_at = nowMinus(minutes + (courseIndex - i) * 8);
    courses[i].served_at = nowMinus(minutes + (courseIndex - i) * 5);
    courses[i].items.forEach((item) => { item.status = 'ready'; });
  }
  if (courses[courseIndex]) {
    courses[courseIndex].status = courseStatus;
    courses[courseIndex].fired_at = nowMinus(minutes);
    if (courseStatus === 'preparing') {
      courses[courseIndex].items.forEach((item, index) => { item.status = index % 3 === 0 ? 'ready' : 'preparing'; });
    } else if (courseStatus === 'ready') {
      courses[courseIndex].items.forEach((item) => { item.status = 'ready'; });
    } else if (courseStatus === 'fired') {
      courses[courseIndex].items.forEach((item) => { item.status = 'fired'; });
    }
  }
  const serviceGuests = guests(pax);
  if (critical && serviceGuests[1]) {
    serviceGuests[1].restrictions.push({ id: id('restriction'), label: 'Marisco', type: 'allergy', severity: 'critical', notes: 'Evitar contaminación cruzada' });
  }
  return {
    id: serviceId,
    table_id: tableId,
    pax,
    status,
    opened_at: nowMinus(minutes + 35),
    menu: menu ? { id: menu.id, name: menu.name, unit_price_cents: menu.price_cents } : null,
    guests: serviceGuests,
    courses,
    consumptions: [],
    payments: [],
  };
}

function seed() {
  return {
    version: 1,
    services: [
      seededService({ id: 'demo-service-1', tableId: 'table-1', pax: 2, menuId: 'menu-experience', status: 'in_service', courseIndex: 3, courseStatus: 'preparing', minutes: 7, critical: true }),
      seededService({ id: 'demo-service-2', tableId: 'table-3', pax: 4, menuId: 'menu-experience', status: 'in_service', courseIndex: 4, courseStatus: 'ready', minutes: 3 }),
      seededService({ id: 'demo-service-3', tableId: 'table-5', pax: 3, menuId: 'menu-season', status: 'in_service', courseIndex: 1, courseStatus: 'fired', minutes: 2 }),
    ],
  };
}

function loadState() {
  try {
    const parsed = JSON.parse(localStorage.getItem(DEMO_KEY) || 'null');
    if (parsed?.version === 1 && Array.isArray(parsed.services)) return parsed;
  } catch {}
  const initial = seed();
  localStorage.setItem(DEMO_KEY, JSON.stringify(initial));
  return initial;
}

let state = loadState();
const persist = () => localStorage.setItem(DEMO_KEY, JSON.stringify(state));
const clone = (value) => structuredClone(value);

function serviceById(serviceId) {
  const service = state.services.find((candidate) => candidate.id === serviceId);
  if (!service) throw new Error('Servicio demo no encontrado.');
  return service;
}

function currentCourse(service) {
  return service.courses.find((course) => ['fired', 'preparing', 'ready'].includes(course.status))
    || service.courses.filter((course) => ['served', 'skipped'].includes(course.status)).at(-1)
    || null;
}

function totals(service) {
  const menuTotal = service.menu ? service.menu.unit_price_cents * service.pax : 0;
  const consumptionTotal = service.consumptions.filter((item) => !item.cancelled).reduce((sum, item) => sum + item.quantity * item.unit_price_cents, 0);
  const subtotal = menuTotal + consumptionTotal;
  const paid = service.payments.reduce((sum, payment) => sum + payment.amount_cents, 0);
  return { subtotal, paid, balance: Math.max(0, subtotal - paid) };
}

function decorated(service) {
  const { subtotal, paid, balance } = totals(service);
  return clone({ ...service, subtotal_cents: subtotal, paid_cents: paid, balance_cents: balance });
}

function restrictionsFor(service, guestPosition) {
  const guest = service.guests.find((candidate) => candidate.position === guestPosition);
  return guest?.restrictions || [];
}

function kds(stationId) {
  const tickets = [];
  for (const service of state.services) {
    const course = service.courses.find((candidate) => ['fired', 'preparing'].includes(candidate.status));
    if (!course) continue;
    const items = course.items.filter((item) => item.station_id === stationId && item.status !== 'cancelled');
    if (!items.length) continue;
    tickets.push({
      service_id: service.id,
      table_id: service.table_id,
      course_id: course.id,
      course_sequence: course.sequence,
      course_name: course.name,
      course_status: course.status,
      fired_at: course.fired_at,
      items: items.map((item) => ({ ...item, restrictions: restrictionsFor(service, item.guest_position) })),
    });
  }
  return { data: clone(tickets) };
}

function board() {
  return {
    data: state.services.filter((service) => !['closed', 'cancelled'].includes(service.status)).map((service) => {
      const course = currentCourse(service);
      return {
        service_id: service.id,
        table_id: service.table_id,
        pax: service.pax,
        status: service.status,
        course: course ? { id: course.id, sequence: course.sequence, name: course.name, status: course.status, fired_at: course.fired_at, served_at: course.served_at } : null,
        critical_restrictions: service.guests.flatMap((guest) => guest.restrictions.filter((restriction) => restriction.severity === 'critical').map((restriction) => `PAX ${guest.position} · ${restriction.label}`)),
      };
    }),
  };
}

function assignMenu(service, menuId) {
  const menu = menus.find((candidate) => candidate.id === menuId);
  if (!menu) throw new Error('Menú demo no encontrado.');
  service.menu = { id: menu.id, name: menu.name, unit_price_cents: menu.price_cents };
  service.courses = instantiateCourses(menu.id, service.pax);
}

function parsePath(path) {
  return path.split('?')[0].split('/').filter(Boolean);
}

export function resetDemo() {
  state = seed();
  persist();
}

export async function demoRequest(path, { method = 'GET', body } = {}) {
  await new Promise((resolve) => setTimeout(resolve, 70));
  const parts = parsePath(path);

  if (path === '/meta') return { app: 'Hospitality OS', mode: 'demo', version: '0.1.0' };
  if (path === '/auth/login' && method === 'POST') return { access_token: 'demo-token', user: { id: 'demo-user', name: 'Usuario Demo', email: 'demo@hospitality.local', permissions: ['*'] } };
  if (path === '/auth/me') return { user: { id: 'demo-user', name: 'Usuario Demo', email: 'demo@hospitality.local', permissions: ['*'] } };
  if (path === '/auth/logout' && method === 'POST') return { ok: true };
  if (path === '/configuration') return { data: clone(configuration) };
  if (path === '/service-board') return board();
  if (parts[0] === 'kds' && parts[1] === 'stations') return kds(parts[2]);

  if (path === '/services' && method === 'POST') {
    const service = {
      id: id('service'),
      table_id: body.table_id,
      pax: Number(body.pax),
      status: 'open',
      opened_at: new Date().toISOString(),
      menu: null,
      guests: guests(Number(body.pax)),
      courses: [],
      consumptions: [],
      payments: [],
    };
    if (body.menu_id) assignMenu(service, body.menu_id);
    state.services.push(service);
    persist();
    return { service_id: service.id };
  }

  if (parts[0] !== 'services' || !parts[1]) throw new Error(`Ruta demo no soportada: ${method} ${path}`);
  const service = serviceById(parts[1]);
  if (parts.length === 2 && method === 'GET') return decorated(service);

  if (parts[2] === 'menu' && method === 'POST') { assignMenu(service, body.menu_id); persist(); return decorated(service); }
  if (parts[2] === 'guests' && parts.length === 3 && method === 'POST') {
    const guest = { id: id('guest'), position: service.guests.length + 1, name: body?.name || null, restrictions: [] };
    service.guests.push(guest); persist(); return clone(guest);
  }
  if (parts[2] === 'guests' && parts[4] === 'restrictions' && method === 'POST') {
    const guest = service.guests.find((candidate) => candidate.id === parts[3]);
    if (!guest) throw new Error('Comensal demo no encontrado.');
    const restriction = { id: id('restriction'), label: body.label, type: body.type, severity: body.severity, notes: body.notes || null };
    guest.restrictions.push(restriction); persist(); return clone(restriction);
  }
  if (parts[2] === 'start' && method === 'POST') { service.status = 'in_service'; persist(); return decorated(service); }
  if (parts[2] === 'pause' && method === 'POST') { service.status = 'paused'; persist(); return decorated(service); }
  if (parts[2] === 'resume' && method === 'POST') { service.status = 'in_service'; persist(); return decorated(service); }
  if (parts[2] === 'courses' && parts[3] === 'fire-next' && method === 'POST') {
    const course = service.courses.find((candidate) => candidate.status === 'pending');
    if (!course) throw new Error('No quedan pases pendientes.');
    if (service.courses.some((candidate) => ['fired', 'preparing', 'ready'].includes(candidate.status))) throw new Error('Hay un pase todavía activo.');
    course.status = 'fired'; course.fired_at = new Date().toISOString(); course.items.forEach((item) => { item.status = 'fired'; }); persist(); return clone(course);
  }
  if (parts[2] === 'courses' && parts[4] === 'ready' && method === 'POST') {
    const course = service.courses.find((candidate) => candidate.id === parts[3]);
    if (!course) throw new Error('Pase no encontrado.');
    if (course.items.some((item) => !['ready', 'cancelled'].includes(item.status))) throw new Error('Todavía hay elaboraciones pendientes.');
    course.status = 'ready'; persist(); return clone(course);
  }
  if (parts[2] === 'courses' && parts[4] === 'serve' && method === 'POST') {
    const course = service.courses.find((candidate) => candidate.id === parts[3]);
    if (!course || course.status !== 'ready') throw new Error('El pase todavía no está listo.');
    course.status = 'served'; course.served_at = new Date().toISOString(); persist(); return clone(course);
  }
  if (parts[2] === 'courses' && parts[4] === 'items' && ['start', 'ready'].includes(parts[6]) && method === 'POST') {
    const course = service.courses.find((candidate) => candidate.id === parts[3]);
    const item = course?.items.find((candidate) => candidate.id === parts[5]);
    if (!course || !item) throw new Error('Elaboración no encontrada.');
    item.status = parts[6] === 'start' ? 'preparing' : 'ready';
    if (course.status === 'fired') course.status = 'preparing';
    persist(); return clone(item);
  }
  if (parts[2] === 'consumptions' && method === 'POST') {
    const item = { id: id('consumption'), name: body.name, quantity: Number(body.quantity), unit_price_cents: Number(body.unit_price_cents), cancelled: false };
    service.consumptions.push(item); persist(); return clone(item);
  }
  if (parts[2] === 'payments' && method === 'POST') {
    const payment = { id: id('payment'), method: body.method, amount_cents: Number(body.amount_cents), recorded_at: new Date().toISOString() };
    service.payments.push(payment);
    if (totals(service).balance <= 0) service.status = 'paid';
    persist(); return clone(payment);
  }
  if (parts[2] === 'close' && method === 'POST') { service.status = 'closed'; persist(); return decorated(service); }

  throw new Error(`Ruta demo no soportada: ${method} ${path}`);
}
