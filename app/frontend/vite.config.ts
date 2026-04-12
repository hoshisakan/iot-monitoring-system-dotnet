import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

const __dirname = path.dirname(fileURLToPath(import.meta.url))

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, 'src'),
    },
  },
  server: {
    proxy: {
      // 與 Pico2WH.Pi5.IIoT.Api launchSettings「http」埠一致（預設 5163）
      '/api': {
        target: 'http://127.0.0.1:5163',
        changeOrigin: true,
      },
    },
  },
})
