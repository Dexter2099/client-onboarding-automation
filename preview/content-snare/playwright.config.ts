import { defineConfig } from '@playwright/test';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('../../', import.meta.url));
const live = process.env.ONBOARDFLOW_LIVE_ORIGIN;
if (live) {
  const target = new URL(live);
  if (
    ![
      'https://dexter-onboardflow.pages.dev',
      'https://onboardflow.hostless.app',
      'https://dexter-onboardflow.de.deplexo.com',
    ].includes(target.origin) &&
    !(target.protocol === 'http:' && ['127.0.0.1', 'localhost'].includes(target.hostname))
  )
    throw new Error('Live verification is restricted to this app or its loopback container.');
}
export default defineConfig({
  testDir: './tests',
  outputDir: root + '/.local/content-snare-test-results',
  fullyParallel: false,
  workers: 1,
  timeout: 120000,
  reporter: 'line',
  use: {
    baseURL: live ?? 'http://127.0.0.1:5622',
    browserName: process.env.ONBOARDFLOW_BROWSER === 'webkit' ? 'webkit' : 'chromium',
    channel: process.env.ONBOARDFLOW_BROWSER === 'chrome' ? 'chrome' : undefined,
    // Local production-container fixture emulates the verified TLS ingress peer.
    // Hosted HTTPS verification sends no scheme override.
    extraHTTPHeaders:
      live && new URL(live).hostname === '127.0.0.1' && process.env.ONBOARDFLOW_LOCAL_PROXY === '1'
        ? { 'X-Forwarded-Proto': 'https' }
        : undefined,
    viewport: { width: 1280, height: 720 },
    reducedMotion: 'reduce',
    screenshot: 'only-on-failure',
    trace: 'retain-on-failure',
  },
  webServer: live
    ? undefined
    : {
        command: 'node node_modules/vite/bin/vite.js --config preview/content-snare/vite.config.ts',
        cwd: root,
        url: 'http://127.0.0.1:5622/l/instructions',
        reuseExistingServer: true,
      },
});
