import { test, expect, open, button } from './fixtures.js';

test('Timbre tells the two speakers apart', async ({ page, problems }) => {
  await open(page, 'Verify Voices');
  await button(page.locator('.tss-card'), 'Compare the recordings').click();
  await expect(page.locator('#alk-verify-a')).toContainText('same voice');
  await expect(page.locator('#alk-verify-b')).toContainText('another voice');
  expect(problems).toEqual([]);
});

test('Lex gives a pronunciation and Spot thresholds for a typed word', async ({ page, problems }) => {
  await open(page, 'Pronunciations');
  await button(page.locator('.tss-card'), 'Phonemize').click();
  await expect(page.locator('.alk-lex-ipa').first()).toContainText('/heɪnoʊvə/');
  await expect(page.locator('#alk-lex-result')).toContainText('thresholds -1.99 after a pause, -1.22 anywhere');
  expect(problems).toEqual([]);
});

test('reports support, the bundled version and self-hosted models, and preloads', async ({ page, problems }) => {
  await open(page, 'Models and Support');
  await expect(page.locator('#alk-support')).toContainText('the models will run');
  await expect(page.locator('#alk-version')).toContainText(/alkove \d+\.\d+\.\d+, (built from source \(sdk\/\)|the npm release), engine sha256 [0-9a-f]{12}/);
  await expect(page.locator('#alk-models')).toContainText('Self-hosted models');

  for (const preloader of await page.locator('.tss-alkove-preload').all()) {
    if ((await preloader.getAttribute('data-ready')) !== 'true') await button(preloader, 'Download').click();
    await expect(preloader).toHaveAttribute('data-ready', 'true');
  }
  expect(problems).toEqual([]);
});
