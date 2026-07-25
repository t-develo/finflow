/**
 * csv-import-touch.spec.js
 *
 * Verifies the touch-reachable path through the CSV import screen
 * (js/pages/csv-import-page.js): tapping "ファイルを選択" must be a real,
 * unblocked tap (regression-guarded the same way as everywhere else), and
 * selecting a file must unlock the "インポート実行" button and lead to a
 * result state.
 *
 * Drag & drop is intentionally NOT tested here: it has no equivalent touch
 * gesture on a real mobile device, so the file-select button is the only
 * touch-accessible path and is what this spec exercises.
 */

'use strict';

const { test, expect } = require('@playwright/test');
const { registerAndLoginViaUi } = require('./helpers/auth');

const SAMPLE_CSV = [
  'date,description,amount,categoryId',
  '2026-07-01,E2Eテストランチ,1200,',
  '2026-07-02,E2Eテストカフェ,450,',
].join('\n');

test.describe('CSV取込画面: タッチ操作での経路', () => {
  test.beforeEach(async ({ page, request, baseURL }) => {
    await registerAndLoginViaUi(page, request, baseURL);
    await page.goto('/expenses/import');
  });

  test('ファイル選択ボタンはタップ可能で、隠れた input[type=file] に到達できる', async ({
    page,
  }) => {
    const fileSelectBtn = page.locator('#file-select-btn');
    await fileSelectBtn.waitFor({ state: 'visible' });

    // Confirm the button itself is genuinely tappable (not covered by an
    // invisible overlay) before relying on it.
    const hitElementId = await page.evaluate(() => {
      const btn = document.getElementById('file-select-btn');
      const rect = btn.getBoundingClientRect();
      const cx = rect.left + rect.width / 2;
      const cy = rect.top + rect.height / 2;
      const hit = document.elementFromPoint(cx, cy);
      return hit ? hit.closest('#file-select-btn')?.id ?? hit.id ?? hit.tagName : null;
    });
    expect(
      hitElementId,
      'ファイル選択ボタンが他の要素に覆われていて、タップが到達しません。'
    ).toBe('file-select-btn');

    await fileSelectBtn.tap();

    // A real OS file picker can't be driven by Playwright; instead we
    // confirm the hidden input this button targets is present in the DOM
    // and accepts file injection, which is the only way a touch user's
    // file picker selection actually reaches this app.
    const fileInput = page.locator('#file-input');
    await expect(fileInput).toBeAttached();

    await fileInput.setInputFiles({
      name: 'e2e-sample.csv',
      mimeType: 'text/csv',
      buffer: Buffer.from(SAMPLE_CSV, 'utf-8'),
    });

    const fileInfo = page.locator('#file-info');
    await expect(fileInfo).toBeVisible();
    await expect(page.locator('#file-name')).toHaveText('e2e-sample.csv');

    const uploadBtn = page.locator('#upload-btn');
    await expect(uploadBtn).toBeEnabled();
  });

  test('CSVファイル選択後、インポート実行ボタンをタップすると結果が表示される', async ({
    page,
  }) => {
    const fileInput = page.locator('#file-input');
    await fileInput.setInputFiles({
      name: 'e2e-sample.csv',
      mimeType: 'text/csv',
      buffer: Buffer.from(SAMPLE_CSV, 'utf-8'),
    });

    const uploadBtn = page.locator('#upload-btn');
    await expect(uploadBtn).toBeEnabled();
    await uploadBtn.tap();

    const resultArea = page.locator('#result-area');
    await expect(resultArea.locator('.alert')).toBeVisible({ timeout: 15_000 });
  });

  test('取り込んだ支出が支出一覧画面に表示される', async ({ page }) => {
    const fileInput = page.locator('#file-input');
    await fileInput.setInputFiles({
      name: 'e2e-sample.csv',
      mimeType: 'text/csv',
      buffer: Buffer.from(SAMPLE_CSV, 'utf-8'),
    });

    await page.locator('#upload-btn').tap();
    await expect(page.locator('#result-area .alert')).toBeVisible({ timeout: 15_000 });

    const viewExpensesLink = page.locator('#result-area a[href="/expenses"]');
    await viewExpensesLink.tap();

    await page.waitForFunction(() => window.location.pathname === '/expenses', null, {
      timeout: 10_000,
    });

    await expect(page.getByText('E2Eテストランチ')).toBeVisible({ timeout: 15_000 });
  });
});
