// Los tests corren en Node (vitest, environment 'node') pero el proyecto no depende de @types/node:
// se declara solo lo que usa install.test.ts, en vez de sumar una dependencia para tres funciones.
declare module 'node:fs' {
  export function readFileSync(path: string): Uint8Array & { toString(encoding: 'utf8'): string }
}
declare module 'node:url' {
  export function fileURLToPath(url: URL): string
}
declare module 'node:zlib' {
  export function inflateSync(data: Uint8Array): Uint8Array
}
