import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5186,
    proxy: {
      '/api': { target: 'http://localhost:5185', changeOrigin: true, ws: true },
      '/auth': { target: 'http://localhost:5185', changeOrigin: true },
    },
  },
})
