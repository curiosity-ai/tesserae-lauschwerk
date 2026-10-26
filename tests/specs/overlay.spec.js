import { test, expect, open, button } from './fixtures.js';

test.use({ micFile: 'wake.wav' });

// The recording has speech from 1.7 s and loops, so the bars move within a few seconds of the overlay opening.
test('the listening overlay opens over the page, draws the bars, shows the words and cancels', async ({ page, problems }) => {
  await open(page, 'Listening Overlay');
  await button(page.locator('.tss-card'), 'Listen').click();

  const overlay = page.locator('.tss-lw-overlay');
  await expect(overlay).toBeVisible();
  await expect(overlay.locator('.tss-lw-overlay-state')).toHaveText('Listening');

  // It sits on top of the page, at the bottom
  const box = await overlay.boundingBox();
  const viewport = page.viewportSize();
  expect(box.y + box.height).toBeGreaterThan(viewport.height - 60);
  expect(await overlay.evaluate((el) => getComputedStyle(el).position)).toBe('fixed');

  // The bars: resting bars are a few pixels tall, speech makes the middle ones tall. Read the canvas through a copy (a readback warning otherwise).
  const tallest = () => overlay.locator('canvas').evaluate((c) => {
    const copy = document.createElement('canvas');
    copy.width = c.width; copy.height = c.height;
    const ctx = copy.getContext('2d', { willReadFrequently: true });
    ctx.drawImage(c, 0, 0);
    const { data } = ctx.getImageData(0, 0, c.width, c.height);
    let best = 0;
    for (let x = 0; x < c.width; x += 2) {
      let top = -1, bottom = -1;
      for (let y = 0; y < c.height; y++) if (data[(y * c.width + x) * 4 + 3] > 0) { if (top < 0) top = y; bottom = y; }
      if (top >= 0) best = Math.max(best, bottom - top + 1);
    }
    return best / (c.height / c.clientHeight);
  });
  await expect.poll(tallest, { timeout: 30_000 }).toBeGreaterThan(30);

  // The words, as they are heard
  await expect(overlay.locator('.tss-lw-overlay-text')).toHaveText('Show me open supplier notices for the A320 line', { timeout: 15_000 });
  await expect(overlay.locator('.tss-lw-overlay-text')).toHaveClass(/tss-lw-overlay-final/);

  // Cancel closes it
  await overlay.getByRole('button', { name: 'Cancel' }).click();
  await expect(overlay).toHaveCount(0, { timeout: 5_000 });
  expect(problems).toEqual([]);
});

test('without the transcript option there is no text line', async ({ page }) => {
  await open(page, 'Listening Overlay');
  await page.locator('#lw-overlay-transcript').click();
  await button(page.locator('.tss-card'), 'Listen').click();

  const overlay = page.locator('.tss-lw-overlay');
  await expect(overlay).toBeVisible();
  await expect(overlay.locator('.tss-lw-overlay-text')).toBeHidden();
});
