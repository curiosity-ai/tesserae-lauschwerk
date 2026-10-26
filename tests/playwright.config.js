import { defineConfig } from '@playwright/test';

// The gallery has to be built first (dotnet build Tesserae.Alkove.Sample); ALKOVE_SITE points at a
// Release build instead. Every spec that needs a microphone launches its own Chromium with a fake capture
// device playing one of the synthesized recordings (see fixtures.js), so the specs run one at a time.
export default defineConfig({
  testDir: './specs',
  timeout: 90_000,
  expect: { timeout: 30_000 },
  workers: 1,
  reporter: [['list']],
  use: {
    baseURL: `http://localhost:${process.env.ALKOVE_PORT ?? 5004}/`,
    trace: 'retain-on-failure',
  },
  webServer: {
    command: 'node serve.mjs',
    url: `http://localhost:${process.env.ALKOVE_PORT ?? 5004}/index.html`,
    reuseExistingServer: true,
  },
});
