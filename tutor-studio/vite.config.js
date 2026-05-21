import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
// Explicit Node import: vite.config.js runs in Node context, but referencing
// the bare `process` global trips ESLint's no-undef in a browser-targeted
// project. `loadEnv` was considered but is heavier than needed for one flag.
import process from 'node:process'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    // 0.0.0.0 binds inside the dev container so host browsers can reach Vite.
    host: '0.0.0.0',
    port: 5173,
    strictPort: true,
    watch: {
      // VITE_USE_POLLING=true is the reliable HMR path on WSL2 and some macOS
      // VirtioFS setups where inotify/fsevents events don't cross the bind mount.
      usePolling: process.env.VITE_USE_POLLING === 'true',
    },
  },
})
