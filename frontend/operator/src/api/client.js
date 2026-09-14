import { DEMO_API_BASE, demoRequest } from './demo.js';
import { clearSession, saveSession, session } from '../state/session.js';

export class ApiError extends Error {
  constructor(message, { status = 0, code = 'request_failed', retryable = false, details = null } = {}) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.code = code;
    this.retryable = retryable;
    this.details = details;
  }
}

function makeHeaders({ auth = true, mutation = false, headers = {} } = {}) {
  const result = {
    Accept: 'application/json',
    'Content-Type': 'application/json',
    'X-Device-Id': session.deviceId,
    ...headers,
  };
  if (auth && session.token) result.Authorization = `Bearer ${session.token}`;
  if (mutation && !result['Idempotency-Key']) result['Idempotency-Key'] = crypto.randomUUID();
  return result;
}

async function request(path, { method = 'GET', body, auth = true, mutation = false, headers } = {}) {
  if (session.apiBase === DEMO_API_BASE) {
    try {
      const payload = await demoRequest(path, { method, body, auth, mutation, headers });
      session.connection = 'demo';
      session.lastConnectedAt = new Date();
      return payload;
    } catch (error) {
      throw new ApiError(error.message || 'Error en el modo demo.', {
        code: 'demo_error',
        details: error,
      });
    }
  }

  let response;
  try {
    response = await fetch(`${session.apiBase}${path}`, {
      method,
      headers: makeHeaders({ auth, mutation, headers }),
      body: body === undefined ? undefined : JSON.stringify(body),
    });
    session.connection = 'online';
    session.lastConnectedAt = new Date();
  } catch (error) {
    session.connection = 'offline';
    throw new ApiError('No se puede conectar con el servidor local.', {
      code: 'network_error',
      retryable: true,
      details: error,
    });
  }

  const text = await response.text();
  let payload = null;
  if (text) {
    try { payload = JSON.parse(text); } catch { payload = { message: text }; }
  }

  if (!response.ok) {
    if (response.status === 401 && auth) {
      clearSession();
      window.dispatchEvent(new CustomEvent('hospitality:auth-lost'));
    }
    const code = payload?.error || (response.status === 422 ? 'validation_error' : 'request_failed');
    const validation = payload?.errors ? Object.values(payload.errors).flat().join(' ') : null;
    throw new ApiError(validation || payload?.message || `Error HTTP ${response.status}`, {
      status: response.status,
      code,
      retryable: Boolean(payload?.retryable),
      details: payload,
    });
  }
  return payload;
}

export const api = {
  meta: () => request('/meta', { auth: false }),
  login: async ({ email, password }) => {
    const body = { email, password, device_name: session.deviceId };
    if (session.companyId) body.company_id = session.companyId;
    if (session.locationId) body.location_id = session.locationId;
    const result = await request('/auth/login', { method: 'POST', body, auth: false });
    saveSession(result.access_token, result.user);
    return result;
  },
  me: () => request('/auth/me'),
  logout: () => request('/auth/logout', { method: 'POST', mutation: false }),
  configuration: () => request('/configuration'),
  board: () => request('/service-board'),
  kds: (stationId) => request(`/kds/stations/${encodeURIComponent(stationId)}`),
  service: (serviceId) => request(`/services/${encodeURIComponent(serviceId)}`),
  openService: (body) => request('/services', { method: 'POST', body, mutation: true }),
  assignMenu: (serviceId, menuId) => request(`/services/${serviceId}/menu`, { method: 'POST', body: { menu_id: menuId }, mutation: true }),
  addGuest: (serviceId, name = null) => request(`/services/${serviceId}/guests`, { method: 'POST', body: { name }, mutation: true }),
  addRestriction: (serviceId, guestId, body) => request(`/services/${serviceId}/guests/${guestId}/restrictions`, { method: 'POST', body, mutation: true }),
  startService: (serviceId) => request(`/services/${serviceId}/start`, { method: 'POST', mutation: true }),
  pauseService: (serviceId, reason = null) => request(`/services/${serviceId}/pause`, { method: 'POST', body: { reason }, mutation: true }),
  resumeService: (serviceId) => request(`/services/${serviceId}/resume`, { method: 'POST', mutation: true }),
  fireNext: (serviceId) => request(`/services/${serviceId}/courses/fire-next`, { method: 'POST', mutation: true }),
  courseReady: (serviceId, courseId) => request(`/services/${serviceId}/courses/${courseId}/ready`, { method: 'POST', mutation: true }),
  serveCourse: (serviceId, courseId) => request(`/services/${serviceId}/courses/${courseId}/serve`, { method: 'POST', mutation: true }),
  startItem: (serviceId, courseId, itemId) => request(`/services/${serviceId}/courses/${courseId}/items/${itemId}/start`, { method: 'POST', mutation: true }),
  readyItem: (serviceId, courseId, itemId) => request(`/services/${serviceId}/courses/${courseId}/items/${itemId}/ready`, { method: 'POST', mutation: true }),

  checkoutService: (serviceId) => request(`/checkout/services/${encodeURIComponent(serviceId)}`),
  checkoutAddConsumption: (serviceId, productId, quantity = 1) => request(`/checkout/services/${serviceId}/consumptions`, {
    method: 'POST',
    body: { product_id: productId, quantity: Number(quantity) },
    mutation: true,
  }),
  checkoutCancelConsumption: (serviceId, consumptionId, reason) => request(`/checkout/services/${serviceId}/consumptions/${consumptionId}/cancel`, {
    method: 'POST',
    body: { reason },
    mutation: true,
  }),
  checkoutAddPayment: (serviceId, body) => request(`/checkout/services/${serviceId}/payments`, { method: 'POST', body, mutation: true }),
  checkoutClose: (serviceId) => request(`/checkout/services/${serviceId}/close`, { method: 'POST', mutation: true }),
};

export async function restoreSession() {
  if (!session.token) {
    session.restored = true;
    return;
  }
  try {
    const result = await api.me();
    saveSession(session.token, result.user);
  } catch {
    clearSession();
  } finally {
    session.restored = true;
  }
}
