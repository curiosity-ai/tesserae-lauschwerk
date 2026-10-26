import { test, expect } from './fixtures.js';

// Every page is built when it is opened and torn down when it is left: walking them all is a check on
// every component's construction and teardown, with the console watched.
test('every page opens with a clean console', async ({ page, problems }) => {
  await page.goto('index.html');
  const names = await page.$$eval('.tss-sidebar-middle .tss-sidebar-btn', (els) => els.map((e) => e.textContent.trim()).filter(Boolean));
  const pages = [...new Set(names)].filter((n) => !/Source Code/.test(n));
  expect(pages.length).toBeGreaterThanOrEqual(10);

  for (const name of pages) {
    await page.goto(`index.html#/view/${encodeURIComponent(name)}`);
    await expect(page.locator('.tss-card').first()).toBeVisible();
  }
  expect(problems).toEqual([]);
});
