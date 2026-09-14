/** Explicit switch from the standalone demo to the real local test server.
 * No session is started and no server data is created/reset by this action.
 */
export function localTestSettings() {
  return {
    apiBase: 'http://127.0.0.1:8000/api/v1',
    companyId: '',
    locationId: '',
    terminalMode: 'main',
  };
}

/** Validate a real connection before persisting it or sending credentials. */
export function normalizeRealApiBase(value) {
  let url;
  try { url = new URL(String(value).trim()); }
  catch { throw new Error('Indica una dirección HTTP o HTTPS válida para el servidor.'); }
  if (!['http:', 'https:'].includes(url.protocol) || url.username || url.password || url.search || url.hash) {
    throw new Error('Usa la dirección HTTP o HTTPS del servidor, sin credenciales ni parámetros.');
  }
  return url.href.replace(/\/+$/, '');
}
