import { defineConfig } from 'vitest/config'
import vue from '@vitejs/plugin-vue'

// La PWA se sirve desde el MISMO origen que la API (el motor la aloja en /app/): sin CORS.
// En desarrollo, un proxy hacia el motor local mantiene esa misma forma.
export default defineConfig({
  base: '/app/',
  plugins: [vue()],
  build: { outDir: 'dist', emptyOutDir: true, sourcemap: false, target: 'es2022' },
  server: { proxy: { '/api': 'http://127.0.0.1:5088', '/health': 'http://127.0.0.1:5088' } },
  test: { environment: 'node', include: ['tests/**/*.test.ts'] },
})
