import { defineConfig } from '@playwright/test';

const connection = process.env.ConnectionStrings__OnboardFlow;
if (
  !connection?.startsWith('Host=127.0.0.1;Port=54329;Database=onboardflow_browser_test_') ||
  !/Database=onboardflow_browser_test_[a-f0-9]{32};/.test(connection)
) {
  throw new Error('Run npm run verify: browser tests require their disposable local database.');
}

export default defineConfig({
  testDir: 'tests/browser',
  workers: 1,
  use: { baseURL: 'http://127.0.0.1:5081', trace: 'retain-on-failure' },
  projects: [
    { name: 'chromium', use: { browserName: 'chromium' } },
    { name: 'firefox', use: { browserName: 'firefox' } },
    { name: 'webkit', use: { browserName: 'webkit' } },
  ],
  reporter: [['list'], ['html', { open: 'never' }]],
  webServer: {
    command: 'pwsh -NoProfile -File scripts/local.ps1 api -Port 5081',
    // This guarded disposable server runs three visitors' suites in seconds.
    // Production rate rejection remains covered separately by ApiSafetyTests.
    env: { ApiLimits__RequestsPerMinute: '10000' },
    url: 'http://127.0.0.1:5081/api/csrf',
    reuseExistingServer: false,
    timeout: 60000,
  },
});
