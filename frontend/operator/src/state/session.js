import { reactive, computed } from 'vue';

const DEFAULT_API = import.meta.env.VITE_API_BASE_URL || 'http://127.0.0.1:8000/api/v1';
const TOKEN_KEY = 'hospitality.session.token';
const USER_KEY = 'hospitality.session.user';
const API_KEY = 'hospitality.device.api';
const COMPANY_KEY = 'hospitality.device.company';
const LOCATION_KEY = 'hospitality.device.location';
const DEVICE_KEY = 'hospitality.device.id';

function deviceId() {
  let value = localStorage.getItem(DEVICE_KEY);
  if (!value) {
    value = `device-${crypto.randomUUID()}`;
    localStorage.setItem(DEVICE_KEY, value);
  }
  return value;
}

function readUser() {
  try {
    return JSON.parse(sessionStorage.getItem(USER_KEY) || 'null');
  } catch {
    return null;
  }
}

export const session = reactive({
  token: sessionStorage.getItem(TOKEN_KEY),
  user: readUser(),
  apiBase: localStorage.getItem(API_KEY) || DEFAULT_API,
  companyId: localStorage.getItem(COMPANY_KEY) || '',
  locationId: localStorage.getItem(LOCATION_KEY) || '',
  deviceId: deviceId(),
  restored: false,
  connection: 'checking',
  lastConnectedAt: null,
});

export const isAuthenticated = computed(() => Boolean(session.token && session.user));

export function hasPermission(permission) {
  const permissions = session.user?.permissions || [];
  return permissions.includes('*') || permissions.includes(permission);
}

export function saveSession(token, user) {
  session.token = token;
  session.user = user;
  sessionStorage.setItem(TOKEN_KEY, token);
  sessionStorage.setItem(USER_KEY, JSON.stringify(user));
}

export function clearSession() {
  session.token = null;
  session.user = null;
  sessionStorage.removeItem(TOKEN_KEY);
  sessionStorage.removeItem(USER_KEY);
}

export function saveDeviceSettings({ apiBase, companyId = '', locationId = '' }) {
  session.apiBase = apiBase.replace(/\/$/, '');
  session.companyId = companyId.trim();
  session.locationId = locationId.trim();
  localStorage.setItem(API_KEY, session.apiBase);
  localStorage.setItem(COMPANY_KEY, session.companyId);
  localStorage.setItem(LOCATION_KEY, session.locationId);
}
