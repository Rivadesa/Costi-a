// D6.6: la credencial del dispositivo y la ORDEN SIN CONFIRMAR viven en IndexedDB, que un navegador puede desalojar bajo
// presion de espacio o tras semanas sin uso (sobre todo iOS con la web sin instalar). Se pide almacenamiento PERSISTENTE;
// el navegador decide (Chrome lo concede a una PWA instalada o muy usada, sin preguntar). Si no lo concede, se dice.
// No es una garantia absoluta: el usuario siempre puede borrar los datos del sitio. La conciliacion por clave desde el
// servidor sigue siendo la red de seguridad de una orden que llego a enviarse.
// B.2 (ADR-014): por la conexion sencilla (http://IP) el navegador no da almacenamiento protegido: se dice tal cual ('plain-http'),
// sin pedir lo que no puede conceder. La aplicacion funciona igual. Revision C3c: el aviso no dice que no se pueda instalar, porque en
// los dos caminos se anade su icono (en iPhone y iPad es donde se empareja) y el aviso sale dentro de ese icono.
export type Persistence = 'persisted' | 'best-effort' | 'unsupported' | 'plain-http'

interface StorageLike { persist?: () => Promise<boolean>; persisted?: () => Promise<boolean> }

export async function requestPersistence(storage: StorageLike | undefined = globalThis.navigator?.storage,
                                         secure: boolean = globalThis.isSecureContext ?? true): Promise<Persistence> {
  if (!secure) return 'plain-http'
  if (!storage || typeof storage.persist !== 'function') return 'unsupported'
  try {
    if (typeof storage.persisted === 'function' && await storage.persisted()) return 'persisted'
    return await storage.persist() ? 'persisted' : 'best-effort'
  } catch { return 'best-effort' }
}

export const PERSISTENCE_HINT: Record<Persistence, string> = {
  persisted: '',
  'best-effort': 'Este navegador puede borrar los datos de esta aplicación si pasa tiempo sin usarse o falta espacio. Instálala en la pantalla de inicio para protegerlos.',
  unsupported: 'Este navegador no ofrece almacenamiento protegido. Instala la aplicación en la pantalla de inicio y no borres los datos del sitio.',
  'plain-http': 'Conexión sencilla (sin cifrar): el navegador no protege los datos de esta aplicación. Funciona igual: ábrela siempre desde su icono, no borres los datos del sitio ni quites el icono, y úsala solo con el Wi-Fi del personal.',
}
