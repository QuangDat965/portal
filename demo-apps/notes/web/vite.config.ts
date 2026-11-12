import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  build: {
    outDir: '../api/wwwroot',
    emptyOutDir: true,
  },
  server: {
    port: 5184,
    proxy: {
      '/api': {
        target: 'http://localhost:5182',
        changeOrigin: true,
      },
      '/auth': {
        target: 'http://localhost:5182',
        changeOrigin: true,
      },
    },
  },
})
