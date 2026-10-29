import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
// Backend: http://localhost:5285  |  Frontend: http://localhost:5174
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5174,
    strictPort: true,
    proxy: {
      '/api': {
        target: 'http://localhost:5285',
        changeOrigin: false,
        ws: true,
      },
    },
  },
})

