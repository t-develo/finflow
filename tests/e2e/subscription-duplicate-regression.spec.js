/**
 * subscription-duplicate-regression.spec.js
 *
 * 「サブスクリプションが二重・三重に登録される」の回帰テスト。
 *
 * ------------------------------------------------------------------
 * 何が起きていたか
 * ------------------------------------------------------------------
 * subscription-page.js の loadAndRender() が、呼ばれるたびに
 * attachEventListeners() を呼び直していた。ところがリスナーの貼り先
 * (#modal-save-btn 等) は buildShell() が 1 度だけ作る**永続要素**なので、
 * 古いリスナーは死なずに積み上がる。しかも増えたリスナーがそれぞれ
 * loadAndRender() を呼ぶため、リスナー数は 1 → 2 → 4 → 8 と指数的に増え、
 * POST 回数がそのまま登録件数になっていた。
 *
 *   1 件目の保存 … POST 1 回（正常）
 *   2 件目の保存 … POST 2 回（二重登録）
 *   3 件目の保存 … POST 4 回（四重登録）
 *
 * ------------------------------------------------------------------
 * なぜ既存テストで見つからなかったか
 * ------------------------------------------------------------------
 * hidden-attribute.spec.js は保存を **1 回しか** 行わず、しかも
 * `[data-action="edit"]` を `.first()` で掴む。行が 2 件でも 3 件でも通る。
 * つまり「2 件目以降を登録する」という、このバグの発現条件そのものを
 * 踏んでいなかった。
 *
 * ここで守る不変条件:
 *   1. 同一ページに留まったまま N 件登録したら、一覧はちょうど N 件になる
 *   2. 1 回の保存で飛ぶ POST /api/subscriptions はちょうど 1 回
 *   3. 保存ボタンのリスナーは何度再描画されても増えない
 */

'use strict';

const { test, expect } = require('@playwright/test');
const { registerAndLoginViaUi } = require('./helpers/auth');
const { subscriptionAddButton } = require('./helpers/subscriptions');

/**
 * モーダルを開いて 1 件登録する。
 * 保存ボタンは tap() で押す（実機と同じ経路を通すため）。
 */
async function addSubscription(page, { name, amount, date }) {
  await subscriptionAddButton(page).tap();
  await page.locator('#subscription-modal').waitFor({ state: 'visible' });

  await page.locator('#service-name').fill(name);
  await page.locator('#amount').fill(String(amount));
  await page.locator('#next-billing-date').fill(date);

  await page.locator('#modal-save-btn').tap();
  await page.locator('#subscription-modal').waitFor({ state: 'hidden' });
}

test.describe('サブスク登録: 二重登録の回帰ガード', () => {
  test.beforeEach(async ({ page, request, baseURL }) => {
    await registerAndLoginViaUi(page, request, baseURL);
    await page.goto('/subscriptions');
    await subscriptionAddButton(page).waitFor();
  });

  test('同じ画面で 3 件続けて登録しても、一覧はちょうど 3 件になる', async ({ page }) => {
    // 実際に飛んだ POST を数える。件数だけを見ると、サーバー側の 409 が
    // 重複を握り潰した場合に「フロントは直っていないのに緑」になるため、
    // リクエスト数そのものを検証する。
    const postCounts = [];
    let currentPosts = 0;
    page.on('request', (req) => {
      if (req.method() === 'POST' && req.url().includes('/api/subscriptions')) {
        currentPosts += 1;
      }
    });

    const services = [
      { name: 'Netflix', amount: 1490, date: '2026-08-15' },
      { name: 'Spotify', amount: 980, date: '2026-08-03' },
      { name: 'GitHub Copilot', amount: 1500, date: '2026-08-21' },
    ];

    for (const service of services) {
      currentPosts = 0;
      await addSubscription(page, service);
      postCounts.push(currentPosts);
    }

    // 不変条件 2: どの回も POST はちょうど 1 回
    // （修正前はここが [1, 2, 4] になっていた）
    expect(postCounts).toEqual([1, 1, 1]);

    // 不変条件 1: 一覧はちょうど 3 件
    await expect(page.locator('.sub-card')).toHaveCount(3);

    // 同じサービス名が 2 つ以上並んでいないこと
    const names = await page.locator('.sub-card__name').allTextContents();
    expect(new Set(names).size).toBe(names.length);
  });

  test('保存を繰り返しても #modal-save-btn のリスナーは増えない', async ({ page }) => {
    // リスナー本数そのものは JS から数えられないので、
    // 「1 クリックで handleSave が何回走ったか」を POST 数で観測する。
    await addSubscription(page, { name: 'Kindle Unlimited', amount: 980, date: '2026-08-10' });

    let posts = 0;
    page.on('request', (req) => {
      if (req.method() === 'POST' && req.url().includes('/api/subscriptions')) posts += 1;
    });

    // 1 件登録したあと（＝修正前ならリスナーが 2 本になっている状態）の 1 回の保存
    await addSubscription(page, { name: 'Notion', amount: 1200, date: '2026-08-12' });

    expect(posts).toBe(1);
    await expect(page.locator('.sub-card')).toHaveCount(2);
  });
});
