/**
 * boot-guard-regression.spec.js
 *
 * 「iPhone で真っ白画面」の回帰テスト。
 *
 * 背景:
 * ビルドステップが無いため js/*.js は 1 ファイル 1 URL で、それぞれ独立した
 * キャッシュエントリになる。index.html の ?v= は <script src> / <link href>
 * にしか効かず、app.js が import する router.js / utils/*.js には伝播しない。
 * その結果、端末上で「新しい router.js + 古い api-client.js」という混在が
 * 成立しうる。
 *
 * 実際に起きた事故:
 * 後から追加した loadingManager.reset() を古い api-client.js が持っておらず、
 * handleRoute() の**最初の実行文**で TypeError。これは container.innerHTML に
 * 触れる前なので、全ルートが何も描画されないまま停止＝真っ白になった。
 * さらに window.onerror が無かったため、実機に手掛かりが一切残らなかった。
 *
 * ここで守る不変条件:
 *   1. 新旧モジュールが混在しても、白画面にはならない（オプショナル呼び出し）
 *   2. どうしても起動できないときは、白画面ではなく理由の見えるエラー画面を出す
 *   3. 自己修復のリロードは無限ループしない
 *   4. 正常系で起動ガードが誤発火しない
 *
 * 認証は不要（すべて未認証の /login で完結する）。
 */

'use strict';

const { test, expect } = require('@playwright/test');

/** 起動エラー画面。role="alert" はフォームエラーでも使われるため専用の目印で特定する。 */
const BOOT_ERROR = '[data-ff-boot-error]';

test.describe('起動ガード: 白画面の回帰ガード', () => {
  test('古い api-client.js が混ざっても、ログイン画面は描画され操作できる', async ({ page }) => {
    // 「reset() が追加される前の api-client.js」を再現する。
    // git の SHA を固定するより、実物から当該メソッドだけを取り除くほうが
    // 履歴の書き換えに強く、意図も明確。loadingManager は IIFE が返す
    // ミュータブルなオブジェクトなので、モジュール末尾で delete できる。
    await page.route('**/js/utils/api-client.js', async (route) => {
      const response = await route.fetch();
      const body = await response.text();
      await route.fulfill({
        response,
        body: body + '\n// E2E: reset() 追加前の版を再現\ndelete loadingManager.reset;\n',
      });
    });

    const pageErrors = [];
    page.on('pageerror', (error) => pageErrors.push(error.message));

    await page.goto('/login');

    // 白画面になっていないこと = ログインフォームが実在して操作できること
    const email = page.locator('#login-email, input[type="email"]').first();
    await expect(email).toBeVisible();
    await email.tap();
    await email.fill('shiro@example.com');
    await expect(email).toHaveValue('shiro@example.com');

    // 起動が最後まで到達していること
    await expect.poll(() => page.evaluate(() => window.__ffBooted === true)).toBe(true);

    // 起動エラー画面は出ない
    await expect(page.locator(BOOT_ERROR)).toHaveCount(0);

    // かつての致命傷（reset is not a function）が出ていないこと
    expect(pageErrors.join('\n')).not.toMatch(/reset is not a function/);
  });

  test('モジュールが取得できないときは、白画面ではなくエラー画面と復旧手段を出す', async ({ page }) => {
    await page.route('**/js/router.js', (route) => route.abort());

    let loadCount = 0;
    page.on('load', () => { loadCount += 1; });

    await page.goto('/login');

    // 1 回だけ自己修復（cache:'reload' + reload）を試し、それでも駄目ならエラー画面
    const panel = page.locator(BOOT_ERROR);
    await expect(panel).toBeVisible({ timeout: 30_000 });

    await expect(panel.locator('h1')).toHaveText(/表示できませんでした/);
    // 実機で原因を知る唯一の手段なので、必ず出ていること
    await expect(panel.locator('pre')).not.toBeEmpty();

    // 復旧ボタン（44px のタップ領域規約を満たすこと）
    const button = panel.locator('button');
    await expect(button).toHaveText(/キャッシュを消して再読み込み/);
    const box = await button.boundingBox();
    expect(box.height).toBeGreaterThanOrEqual(44);

    // 自己修復が無限ループしていないこと（初回 + 自己修復の 1 回まで）
    expect(loadCount).toBeLessThanOrEqual(2);
  });

  test('正常系では起動ガードが誤発火しない（ウォッチドッグ経過後も）', async ({ page }) => {
    await page.goto('/login');

    await expect.poll(() => page.evaluate(() => window.__ffBooted === true)).toBe(true);

    // ウォッチドッグ(8秒)を確実に超えてから確認する
    await page.waitForTimeout(9_000);
    await expect(page.locator(BOOT_ERROR)).toHaveCount(0);

    // 正常起動後は自己修復フラグが残らない（次の障害でまた自己修復できる）
    const flag = await page.evaluate(() => sessionStorage.getItem('ff_selfheal'));
    expect(flag).toBeNull();
  });
});
