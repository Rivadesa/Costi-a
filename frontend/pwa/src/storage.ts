// D6.6: la credencial del dispositivo y la ORDEN SIN CONFIRMAR viven en IndexedDB, que un navegador puede desalojar bajo
// presion de espacio o tras semanas sin uso (sobre todo iOS con la web sin instalar). Se pide almacenamiento PERSISTENTE;
// el navegador decide (Chrome lo concede a una PWA instalada o muy usada, sin preguntar). Si no lo concede, se dice.
// No es una garantia absoluta: el usuario siempre puede borrar los datos del sitio. La conciliacion por clave desde el
// servidor sigue siendo la red de seguridad de una orden que llego a enviarse.
export type Persistence = 'persisted' | 'best-effort' | 'unsupported'

interface StorageLike { persist?: () => Promise<boolean>; persisted?: () => Promise<boolean> }

export async function requestPersistence(storage: StorageLike | undefined = globalThis.navigator?.storage): Promise<Persistence> {
  if (!storage || typeof storage.persist !== 'function') return 'unsupported'
  try {
    if (typeof storage.persisted === 'function' && await storage.persisted()) return 'persisted'
    return await storage.persist() ? 'persisted' : 'best-effort'
  } catch { return 'best-effort' }
}

export const PERSISTENCE_HINT: Record<Persistence, string> = {
  persisted: '',
  'best-effort': 'Este navegador puede borrar los datos de esta aplicacion si pasa tiempo sin usarse o falta espacio. Instalala en la pantalla de inicio para protegerlos.',
  unsupported: 'Este navegador no ofrece almacenamiento protegido. Instala la aplicacion en la pantalla de inicio y no borres los datos del sitio.',
}
