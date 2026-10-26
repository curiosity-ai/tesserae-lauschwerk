import { test, expect, open, button } from './fixtures.js';

test.use({ micFile: 'speech.wav' });

test('an agent input streams packets and cuts one utterance per sentence', async ({ page, problems }) => {
  await open(page, 'Agent Input');
  await button(page.locator('.tss-card'), 'Start').click();

  await expect(page.locator('.lw-utterance')).toHaveCount(2, { timeout: 30_000 });
  await expect(page.locator('#lw-agent-packets')).not.toHaveText('0 packets');
  expect(problems).toEqual([]);
});
