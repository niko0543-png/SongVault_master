import { fileURLToPath, URL } from 'node:url'

import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import vueDevTools from 'vite-plugin-vue-devtools'
import { alphaTab } from '@coderline/alphatab-vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    vue(),
    vueDevTools(),
    alphaTab(),             // #28 : worker, AudioWorklet, police et sons d'alphaTab (copiés dans /font et /soundfont)
  ],
  // #28 : le worker et l'AudioWorklet d'alphaTab sont des modules ES (import.meta.url) qui partagent son code
  worker: { format: 'es' },
  // alphaTab pèse 1,1 Mo à lui seul, dans un morceau chargé à la demande : seuls les autres morceaux sont surveillés
  build: { chunkSizeWarningLimit: 1200 },
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
    server: {
    proxy: {
      '/api': { target: 'http://localhost:5080', changeOrigin: true },
    },
  },
})