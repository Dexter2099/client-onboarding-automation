import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { fileURLToPath } from 'node:url';

export default defineConfig({
  root: fileURLToPath(new URL('./preview/content-snare/', import.meta.url)),
  publicDir: fileURLToPath(new URL('./public/', import.meta.url)),
  plugins: [react()],
  server: {
    host: '127.0.0.1',
    port: 5173,
    strictPort: true,
    proxy: { '/api': 'http://127.0.0.1:5080' },
  },
  build: {
    outDir: fileURLToPath(new URL('./src/OnboardFlow.Api/wwwroot/', import.meta.url)),
    emptyOutDir: true,
  },
});
