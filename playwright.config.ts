import { defineConfig } from '@playwright/test';

if (process.env.ONBOARDFLOW_BROWSER_FIXTURE !== '1')
  throw new Error(
    'Run npm run verify: browser checks use the disposable C# integration-test host.',
  );

const origin = new URL(process.env.ONBOARDFLOW_BROWSER_ORIGIN ?? '');
if (origin.protocol !== 'http:' || !['127.0.0.1', 'localhost', '[::1]'].includes(origin.hostname))
  throw new Error('Browser CI requires its loopback Kestrel host.');

export default defineConfig({
  testDir: 'preview/content-snare/tests',
  workers: 1,
  timeout: 120000,
  outputDir: '.local/reports/intake-browser',
  use: { baseURL: origin.origin, trace: 'retain-on-failure', reducedMotion: 'reduce' },
  projects: [
    { name: 'chromium', use: { browserName: 'chromium' } },
    { name: 'firefox', use: { browserName: 'firefox' } },
    { name: 'webkit', use: { browserName: 'webkit' } },
  ],
  reporter: [['list'], ['html', { open: 'never' }]],
});
