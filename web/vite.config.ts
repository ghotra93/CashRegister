import react from '@vitejs/plugin-react';
import { defineConfig } from 'vitest/config';

/** The CashRegister.Api dev URL (src/CashRegister.Api/Properties/launchSettings.json). */
const apiTarget = 'http://localhost:5080';

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    strictPort: true,
    proxy: {
      '/api': apiTarget,
      '/health': apiTarget,
    },
  },
  test: {
    // happy-dom rather than jsdom: jsdom's File/FormData cannot be sent by Node's fetch,
    // which breaks multipart uploads under test. happy-dom ships a consistent set.
    environment: 'happy-dom',
    environmentOptions: { happyDOM: { url: 'http://localhost:5173' } },
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
    restoreMocks: true,
  },
});
