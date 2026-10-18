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
  use: { baseURL: 'http://127.0.0.1:5081', browserName: 'chromium', trace: 'retain-on-failure' },
  reporter: [['list'], ['html', { open: 'never' }]],
  webServer: {
    command: 'pwsh -NoProfile -File scripts/local.ps1 api -Port 5081',
    url: 'http://127.0.0.1:5081/api/csrf',
    reuseExistingServer: false,
    timeout: 60000,
  },
});
