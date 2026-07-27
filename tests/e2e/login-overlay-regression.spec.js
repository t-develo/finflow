/**
 * login-overlay-regression.spec.js
 *
 * ログイン画面が「見た目は普通なのにタップが一切効かない」状態になる経路を
 * 個別に塞いだことを確認する回帰テスト。
 *
 * 背景:
 * `.sidebar-overlay` に pointer-events:none を入れる修正 (3eddd1f) を入れた
 * 後も iPhone 実機で症状が再発した。原因を洗い直したところ、
 * 「非表示状態の .sidebar-overlay」以外にも、ログイン画面を全画面で覆って
 * タップを吸い込む経路が複数残っていた:
 *
 *   1. ドロワー展開中にログアウト/401/popstate で遷移すると
 *      `.sidebar-overlay--visible`（pointer-events:auto）が残る
 *   2. `.loading-overlay`（position:fixed; inset:0; z-index:500）が
 *      document.body 直下に残ると、ルート遷移しても消えない
 *
 * どちらも「透明〜半透明の全画面要素が最前面に居座る」という同じ壊れ方を
 * するため、hidden 状態の pointer-events 修正では防げていなかった。
 *
 * すべての操作は click() ではなく tap() を使う（既存スイートと同じ理由:
 * click() ではオーバーレイによるタップ吸い込みが再現しないことがある）。
 */

'use strict';

const { test, expect } = require('@playwright/test');
const { registerAndLoginViaUi } = require('./helpers/auth');

test.describe('ログイン画面: 全画面オーバーレイの回帰ガード', () => {
  test('ドロワーを開いたままログアウトしても、ログイン画面が操作できる', async ({
    page,
    request,
    baseURL,
  }) => {
    await registerAndLoginViaUi(page, request, baseURL);

    // ハンバーガーからドロワーを開く
    const hamburger = page.locator('#hamburger-btn');
    await hamburger.waitFor({ state: 'visible' });
    await hamburger.tap();

    const overlay = page.locator('#sidebar-overlay');
    await expect(
      overlay,
      'ドロワーを開いても overlay が --visible にならない（前提条件が崩れている）'
    ).toHaveClass(/sidebar-overlay--visible/);

    // ログアウトボタンはサイドバー内にあるが [data-navigo] ではないため、
    // 「サイドバー内のリンクをタップしたら閉じる」既存ハンドラの対象外。
    // ここが overlay を残したまま /login へ遷移していた経路。
    await page.locator('#logout-btn').tap();
    await page.waitForFunction(() => window.location.pathname === '/login', null, {
      timeout: 10_000,
    });

    await expect(
      overlay,
      'ログアウト後もサイドバーオーバーレイが表示状態のまま残っている。' +
        'ログイン画面の全タップを吸い込む状態。'
    ).not.toHaveClass(/sidebar-overlay--visible/);

    await expect(
      page.locator('body'),
      'ログアウト後も body のスクロールロックが残っている'
    ).not.toHaveClass(/body--drawer-open/);

    // 実際にタップが通ることまで確認する
    const emailInput = page.locator('#login-email');
    await emailInput.waitFor({ state: 'visible' });
    await emailInput.tap();
    await expect(emailInput).toBeFocused();
  });

  test('ブラウザバックでログイン画面に戻ってもオーバーレイが残らない', async ({
    page,
    request,
    baseURL,
  }) => {
    await registerAndLoginViaUi(page, request, baseURL);

    const hamburger = page.locator('#hamburger-btn');
    await hamburger.waitFor({ state: 'visible' });
    await hamburger.tap();

    const overlay = page.locator('#sidebar-overlay');
    await expect(overlay).toHaveClass(/sidebar-overlay--visible/);

    // popstate 経由の遷移。router.js は popstate でも handleRoute を通るが、
    // 以前はドロワー状態を一切リセットしていなかった。
    await page.goBack();
    await page.waitForFunction(() => window.location.pathname === '/login', null, {
      timeout: 10_000,
    });

    await expect(
      overlay,
      'popstate 経由の遷移でサイドバーオーバーレイが残っている'
    ).not.toHaveClass(/sidebar-overlay--visible/);

    const emailInput = page.locator('#login-email');
    await emailInput.waitFor({ state: 'visible' });
    await emailInput.tap();
    await expect(emailInput).toBeFocused();
  });

  test('固着したローディングオーバーレイはルート遷移で除去される', async ({ page }) => {
    await page.goto('/login');
    await page.locator('#login-email').waitFor({ state: 'visible' });

    // 応答が返らない API 呼び出しで overlay が出しっぱなしになった状態を再現する。
    // .loading-overlay は document.body 直下に付くため、#page-container を
    // クリアするだけのルート遷移では消えなかった。
    await page.evaluate(async () => {
      const { loadingManager } = await import('/js/utils/api-client.js');
      loadingManager.show();
    });

    await expect(
      page.locator('.loading-overlay'),
      'テストの前提が崩れている: loadingManager.show() で overlay が出ていない'
    ).toHaveCount(1);

    await page.locator('a[href="/register"][data-navigo]').tap();
    await page.waitForFunction(() => window.location.pathname === '/register', null, {
      timeout: 10_000,
    });

    await expect(
      page.locator('.loading-overlay'),
      'ルート遷移後もローディングオーバーレイが残っている。' +
        '画面全体が操作不能になる（z-index:500 の全画面要素）。'
    ).toHaveCount(0);

    const nameInput = page.locator('#reg-name');
    await nameInput.tap();
    await expect(nameInput).toBeFocused();
  });

  test('ログイン画面にはローディングオーバーレイが存在しない', async ({ page }) => {
    await page.goto('/login');
    await page.locator('#login-email').waitFor({ state: 'visible' });

    await expect(page.locator('.loading-overlay')).toHaveCount(0);

    // 入力欄の中心を実際にヒットテストして、最前面が入力欄自身であることを確認
    const topElementId = await page.evaluate(() => {
      const rect = document.querySelector('#login-email').getBoundingClientRect();
      const el = document.elementFromPoint(
        rect.left + rect.width / 2,
        rect.top + rect.height / 2
      );
      return el?.id ?? null;
    });

    expect(
      topElementId,
      'メールアドレス欄の中心をヒットテストしたところ、別の要素が最前面にある'
    ).toBe('login-email');
  });
});

test.describe('index.html: 外部CDNによるレンダーブロックの回帰ガード', () => {
  test('head に同期的な CDN <script> が無い', async ({ request, baseURL }) => {
    const response = await request.get(`${baseURL}/index.html`);
    expect(response.ok()).toBe(true);

    const html = await response.text();
    const headHtml = html.slice(0, html.indexOf('</head>'));

    // defer/async の無い外部 <script> は、ネットワークに出られない環境
    // （LAN 内のラズパイ運用）で DNS/TCP タイムアウトまで <body> の解析を
    // 止めてしまい、その間ページ全体がタップに反応しなくなる。
    const blockingExternalScripts = [
      ...headHtml.matchAll(/<script\b(?![^>]*\b(?:defer|async|type="module")\b)[^>]*\bsrc="https?:\/\/[^"]+"[^>]*>/g),
    ].map((m) => m[0]);

    expect(
      blockingExternalScripts,
      'head に同期読み込みの外部スクリプトがある。オフライン環境で' +
        'ページ全体が操作不能になる時間が発生する。'
    ).toEqual([]);
  });

  test('CSS/JS の参照にキャッシュ破棄用の ?v= が付いている', async ({ request, baseURL }) => {
    const response = await request.get(`${baseURL}/index.html`);
    const html = await response.text();

    const assetRefs = [...html.matchAll(/(?:href|src)="\/(?:css|js)\/[^"]+"/g)].map((m) => m[0]);
    expect(assetRefs.length, 'index.html から CSS/JS の参照が見つからない').toBeGreaterThan(0);

    const missingVersion = assetRefs.filter((ref) => !ref.includes('?v='));
    expect(
      missingVersion,
      'バージョンクエリの無い CSS/JS 参照がある。端末に焼き付いた古い ' +
        'キャッシュが使われ続け、修正が実機に反映されない原因になる。'
    ).toEqual([]);
  });
});
