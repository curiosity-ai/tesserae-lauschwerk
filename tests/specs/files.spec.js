import { test, expect, open, button } from './fixtures.js';

// The models over whole recordings, in lauschwerk's background worker. The fixtures were made to give
// exact answers (tools/Lauschwerk.TestAudio): two phrases, one wake, nothing in noise.
test.describe('Process File', () => {
  test('finds the two phrases of speech.wav', async ({ page, problems }) => {
    await open(page, 'Process File');
    const processor = page.locator('#lw-file-processor');
    await button(page.locator('.tss-card'), 'Speech: two sentences').click();
    await expect(processor).toHaveAttribute('data-state', 'done');
    await expect(processor).toHaveAttribute('data-phrases', '2');
    await expect(processor.locator('.tss-lw-file-status')).toContainText('speech.wav: 9.3 s, 2 phrases');
    expect(problems).toEqual([]);
  });

  test('hears the wake word once in wake.wav, and nothing in noise.wav', async ({ page, problems }) => {
    await open(page, 'Process File');
    const processor = page.locator('#lw-file-processor');

    await button(page.locator('.tss-card'), 'Wake word, then a question').click();
    await expect(processor).toHaveAttribute('data-state', 'done');
    await expect(processor).toHaveAttribute('data-wakes', '1');

    await button(page.locator('.tss-card'), 'Noise only').click();
    await expect(processor).toHaveAttribute('data-phrases', '0');
    await expect(processor).toHaveAttribute('data-state', 'done');
    expect(problems).toEqual([]);
  });

  test('draws the before and after waveforms and offers the download', async ({ page, problems }) => {
    await open(page, 'Process File');
    await button(page.locator('.tss-card'), 'A voice over another voice and noise').click();
    const processor = page.locator('#lw-file-processor');
    await expect(processor).toHaveAttribute('data-state', 'done');

    // Something was drawn: the canvases are sized and not blank.
    const inked = await page.$$eval('#lw-file-processor .tss-lw-waveform canvas', (canvases) => canvases.map((c) => {
      const d = c.getContext('2d').getImageData(0, 0, c.width, c.height).data;
      let n = 0;
      for (let i = 3; i < d.length; i += 4) if (d[i] > 0) n++;
      return n;
    }));
    expect(inked.length).toBe(2);
    for (const n of inked) expect(n).toBeGreaterThan(1000);

    const download = page.waitForEvent('download');
    await button(processor, 'Download WAV').click();
    expect((await download).suggestedFilename()).toBe('mix.lauschwerk.wav');
    expect(problems).toEqual([]);
  });

  test("with the user's voiceprint, Focus drops the other speaker", async ({ page, problems }) => {
    await open(page, 'Process File');
    const processor = page.locator('#lw-file-processor');
    await button(page.locator('.tss-card'), 'A voice over another voice and noise').click();
    // Without a print the other speaker, alone in the user's pause, is the closest voice: one long phrase.
    await expect(processor).toHaveAttribute('data-phrases', '1');

    await page.locator('#lw-only-user').click();
    // With it, only the user's two sentences are left.
    await expect(processor).toHaveAttribute('data-phrases', '2');
    expect(problems).toEqual([]);
  });
});
