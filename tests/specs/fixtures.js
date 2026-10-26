import { test as base, expect, chromium } from '@playwright/test';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const audio = join(dirname(fileURLToPath(import.meta.url)), '..', '..', 'Tesserae.Alkove.Sample', 'assets', 'audio');

// Console lines that are not failures. Every page is checked against this list after its test.
const EXPECTED_NOISE = [/favicon\.ico/];

/**
 * `micFile` names the recording Chromium plays as the microphone (it loops). `page` comes from a
 * browser launched with it, the microphone permission granted, and every console error and uncaught
 * exception collected into `problems`, which each test asserts empty at its end.
 */
export const test = base.extend({
  micFile: ['speech.wav', { option: true }],

  // Test-scoped (Playwright's own `browser` is per worker): each test gets the recording it names.
  micBrowser: async ({ micFile }, use) => {
    const browser = await chromium.launch({
      args: [
        '--use-fake-ui-for-media-stream',
        '--use-fake-device-for-media-stream',
        `--use-file-for-fake-audio-capture=${join(audio, micFile)}`,
        '--autoplay-policy=no-user-gesture-required',
      ],
    });
    await use(browser);
    await browser.close();
  },

  context: async ({ micBrowser, baseURL }, use) => {
    const context = await micBrowser.newContext({ baseURL, permissions: ['microphone'] });
    await use(context);
    await context.close();
  },

  problems: async ({ page }, use) => {
    const problems = [];
    page.on('console', (m) => {
      if ((m.type() === 'error' || m.type() === 'warning') && !EXPECTED_NOISE.some((r) => r.test(m.text()))) problems.push(`${m.type()}: ${m.text()}`);
    });
    page.on('pageerror', (e) => problems.push(`pageerror: ${e.message}`));
    page.on('requestfailed', (r) => problems.push(`requestfailed: ${r.url()}`));
    await use(problems);
  },
});

export { expect };

/** Opens a gallery page by its sidebar name. */
export async function open(page, name) {
  await page.goto(`index.html#/view/${encodeURIComponent(name)}`);
  await expect(page.locator('.tss-card').first()).toBeVisible();
}

/** The scoped button inside an element - the sidebar holds same-named entries. */
export const button = (scope, name) => scope.locator('.tss-btn').filter({ hasText: new RegExp(`^${name}$`) }).first();
