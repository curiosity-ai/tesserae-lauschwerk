import { test, expect, open, button } from './fixtures.js';

test.use({ micFile: 'speech.wav' });

test('an agent input streams packets and cuts one utterance per sentence', async ({ page, problems }) => {
  await open(page, 'Agent Input');
  await button(page.locator('.tss-card'), 'Start').click();

  await expect(page.locator('.lw-utterance')).toHaveCount(2, { timeout: 30_000 });
  await expect(page.locator('#lw-agent-packets')).not.toHaveText('0 packets');
  expect(problems).toEqual([]);
});

test('the live waveform scrolls in what the agent input hears', async ({ page, problems }) => {
  await open(page, 'Agent Input');
  await button(page.locator('.tss-card'), 'Start').click();

  // Pixels are read through a scratch canvas: getImageData on the component's own canvas makes Chromium warn
  // about readback. Returns how many pixels are inked and how many rows of the right quarter hold ink.
  const ink = () => page.locator('#lw-agent-live canvas').evaluate((c) => {
    const copy = document.createElement('canvas');
    copy.width = c.width;
    copy.height = c.height;
    const ctx = copy.getContext('2d', { willReadFrequently: true });
    ctx.drawImage(c, 0, 0);
    const { data } = ctx.getImageData(0, 0, c.width, c.height);
    const from = Math.floor(c.width * 0.75);
    let pixels = 0, rows = 0;
    for (let y = 0; y < c.height; y++) {
      let row = false;
      for (let x = 0; x < c.width; x++) {
        if (data[(y * c.width + x) * 4 + 3] > 0) { pixels++; if (x >= from) row = true; }
      }
      if (row) rows++;
    }
    return { pixels, rows };
  });

  // Columns are drawn only for audio that has arrived, so ink means packets were fed in.
  await expect.poll(async () => (await ink()).pixels, { timeout: 30_000 }).toBeGreaterThan(50);

  // The newest column is at the right edge. Silence draws a 1px line there, speech is taller.
  await expect.poll(async () => (await ink()).rows, { timeout: 30_000 }).toBeGreaterThan(8);
  expect(problems).toEqual([]);
});
