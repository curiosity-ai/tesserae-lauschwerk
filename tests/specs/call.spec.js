import { test, expect, open, button } from './fixtures.js';

test.use({ micFile: 'speech.wav' });

test('a call input runs Focus and Cue on the microphone', async ({ page, problems }) => {
  await open(page, 'Call Input');
  const panel = page.locator('#alk-call-panel');
  await button(panel, 'Start').click();

  await expect(panel.locator('.tss-alkove-mode')).toHaveAttribute('data-mode', 'focus');
  // speech.wav's first phrase is 1.06 - 3.58 s.
  await expect(page.locator('.alk-call-phrase').first()).toContainText(/phrase 1\.\d+ - 3\.\d+ s/, { timeout: 20_000 });

  // The processed track is live: the second meter moves.
  await expect.poll(async () => Number(await panel.locator('.tss-alkove-meter').nth(1).getAttribute('aria-valuenow')), { timeout: 10_000 }).toBeGreaterThan(-60);

  await button(panel, 'Stop').click();
  await expect(panel.locator('.tss-alkove-mode')).toHaveAttribute('data-mode', 'off');
  expect(problems).toEqual([]);
});
