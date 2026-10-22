import { fileURLToPath } from 'node:url'
import { defineConfig } from 'vitest/config'
import vue from '@vitejs/plugin-vue'

// La PWA se sirve desde el MISMO origen que la API (el motor la aloja en /app/): sin CORS.
// En desarrollo, un proxy hacia el motor local mantiene esa misma forma.
// C3c: UNA compilacion con TRES paginas: el enrutador /app/ (index.html) y las dos aplicaciones, Costina Sala (sala/index.html ->
// /app/sala/) y Costina Cocina (cocina/index.html -> /app/cocina/). Lo comun va a /app/assets/ con hash (inmutable, PwaHosting).
const page = (path: string) => fileURLToPath(new URL(path, import.meta.url))
export default defineConfig({
  base: '/app/',
  plugins: [vue()],
  build: {
    outDir: 'dist', emptyOutDir: true, sourcemap: false, target: 'es2022',
    rolldownOptions: { input: { router: page('./index.html'), sala: page('./sala/index.html'), cocina: page('./cocina/index.html') } },
  },
  server: { proxy: { '/api': { target: 'http://127.0.0.1:5088', ws: true }, '/health': 'http://127.0.0.1:5088' } },
  test: { environment: 'node', include: ['tests/**/*.test.ts'] },
})
