import { test, expect, open, button } from './fixtures.js';

test.describe('voiceprint', () => {
  test.use({ micFile: 'enroll.wav' });

  test('learns a voiceprint from the user reading, and remembers it', async ({ page, problems }) => {
    await open(page, 'Voiceprint Enrollment');
    const enroll = page.locator('#lw-print-enroll');
    await button(enroll, 'Start reading').click();
    await expect(enroll).toHaveAttribute('data-progress', '100', { timeout: 45_000 });
    await expect(page.locator('#lw-print-status')).toContainText('s of voice, model lauschwerk-timbre');

    await page.reload();
    await expect(page.locator('#lw-print-enroll .tss-lw-enroll-status')).toContainText('Remembered voiceprint');

    await button(enroll, 'Forget my voice').click();
    expect(await page.evaluate(() => localStorage.getItem('tss-lw-sample-voiceprint'))).toBeNull();
    expect(problems).toEqual([]);
  });
});

test.describe('wake word enrollment', () => {
  test.use({ micFile: 'wake-takes.wav' });

  test('counts three takes and calibrates the word', async ({ page, problems }) => {
    await open(page, 'Word Enrollment');
    const panel = page.locator('#lw-enroll-word-panel');
    await button(panel, 'Enroll').click();
    await expect(panel).toHaveAttribute('data-enrolled', 'true', { timeout: 45_000 });
    await expect(panel.locator('.tss-lw-takes')).toContainText('✓');
    await expect(panel.locator('.tss-lw-enroll-word-status')).toContainText('Enrolled: thresholds');
    expect(problems).toEqual([]);
  });
});
