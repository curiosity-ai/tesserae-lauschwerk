import { test, expect, open, button } from './fixtures.js';

test.describe('wake word on the microphone', () => {
  test.use({ micFile: 'wake.wav' });

  test('wakes on "Hey Nova" after a pause', async ({ page, problems }) => {
    await open(page, 'Wake Word');
    const panel = page.locator('#lw-wake-panel');
    await button(panel, 'Start listening').click();
    await expect(panel.locator('.tss-lw-wake-status')).toContainText('Listening for "Hey Nova"');
    await expect(panel).toHaveAttribute('data-wakes', /^[1-9]/, { timeout: 30_000 });
    await expect(panel.locator('.tss-lw-wake-entry').first()).toContainText('Hey Nova');
    await button(panel, 'Stop').click();
    expect(problems).toEqual([]);
  });
});

test.describe('no wake in noise', () => {
  test.use({ micFile: 'noise.wav' });

  test('stays quiet', async ({ page, problems }) => {
    await open(page, 'Wake Word');
    const panel = page.locator('#lw-wake-panel');
    await button(panel, 'Start listening').click();
    await expect(panel.locator('.tss-lw-wake-status')).toContainText('Listening for "Hey Nova"');
    await page.waitForTimeout(8_000);
    await expect(panel).not.toHaveAttribute('data-wakes', /.*/);
    expect(problems).toEqual([]);
  });
});

test.describe('wake word on a call', () => {
  test.use({ micFile: 'wake.wav' });

  test('Spot behind Focus on the call input', async ({ page, problems }) => {
    await open(page, 'Call Wake Words');
    await button(page.locator('.tss-card'), 'Start').click();
    await expect(page.locator('#lw-call-wake-status')).toContainText('Listening for');
    await expect(page.locator('.lw-call-wake').first()).toBeVisible({ timeout: 30_000 });
    expect(problems).toEqual([]);
  });
});
