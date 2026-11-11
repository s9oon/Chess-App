import react, { reactCompilerPreset } from '@vitejs/plugin-react'
import babel from '@rolldown/plugin-babel'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    react(),
    babel({ presets: [reactCompilerPreset()] })
  ],
  server: {
    // Local dev: forward API calls to the backend, so the browser sees one origin (no CORS)
    proxy: {
      '/Health': 'http://localhost:5242',
    },
  },
})
