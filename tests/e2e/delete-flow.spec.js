/**
 * delete-flow.spec.js
 *
 * 「削除ボタンだけ反応が鈍い / 押しても何も起きない」の回帰テスト。
 *
 * ------------------------------------------------------------------
 * 何が起きていたか
 * ------------------------------------------------------------------
 * subscription-page.js の showDeleteConfirm() は、確認 UI を出すために
 * `.modal__footer` の innerHTML を**文字列で退避 → 文字列から復元**していた。
 * innerHTML による復元は要素を作り直すので、復元後の
 * #modal-save-btn / #modal-cancel-btn / #modal-delete-btn は
 * **リスナーを 1 本も持たない別物**になる。
 * つまり「削除 → キャンセル」を一度でも操作すると、以後そのモーダルの
 * 保存も削除も完全に無反応になっていた。
 *
 * さらに確認ダイアログ(ff-confirm-dialog)は Shadow DOM のため、
 * main.css のモバイル 44px 規約が届かず、実測 34px しかなかった。
 * 既存の tap-target-size.spec.js は document.querySelectorAll を使っており
 * Shadow DOM を貫通しないので、この不足を検出できていなかった。
 *
 * ここで守る不変条件:
 *   1. 削除 → キャンセル のあとでも、削除ボタンはもう一度反応する
 *   2. 削除を実行すると実際に 1 件消え、DELETE は 1 回だけ飛ぶ
 *   3. 確認ダイアログのボタンは Shadow DOM の中でもタップ領域を満たす
 */

'use strict';

const { test, expect } = require('@playwright/test');
const { registerAndLoginViaUi } = require('./helpers/auth');
const { subscriptionAddButton } = require('./helpers/subscriptions');
const { tapTargetIssuesDeep, MIN_TAP_TARGET_PX } = require('./helpers/shadow-dom');

/** 確認ダイアログ（Shadow DOM）の中のボタンを取る。 */
function dialogButton(page, cls) {
  return page.locator(`ff-confirm-dialog .${cls}`);
}

async function seedSubscription(page, name) {
  await subscriptionAddButton(page).tap();
  await page.locator('#subscription-modal').waitFor({ state: 'visible' });
  await page.locator('#service-name').fill(name);
  await page.locator('#amount').fill('1000');
  await page.locator('#next-billing-date').fill('2026-09-01');
  await page.locator('#modal-save-btn').tap();
  await page.locator('#subscription-modal').waitFor({ state: 'hidden' });
}

test.describe('削除フロー: 反応しない削除ボタンの回帰ガード', () => {
  test.beforeEach(async ({ page, request, baseURL }) => {
    await registerAndLoginViaUi(page, request, baseURL);
    await page.goto('/subscriptions');
    await subscriptionAddButton(page).waitFor();
    await seedSubscription(page, 'Disney+');
  });

  test('削除 → キャンセル のあとでも、もう一度削除できる', async ({ page }) => {
    const deleteBtn = page.locator('.sub-card [data-action="delete"]').first();

    // 1 回目: 確認を出してキャンセルする
    await deleteBtn.tap();
    await expect(dialogButton(page, 'dialog__confirm-btn')).toBeVisible();
    await dialogButton(page, 'dialog__cancel-btn').tap();
    await expect(dialogButton(page, 'dialog__confirm-btn')).toBeHidden();

    // キャンセルしたので消えていない
    await expect(page.locator('.sub-card')).toHaveCount(1);

    // 2 回目: ここが修正前は無反応だった
    await deleteBtn.tap();
    await expect(dialogButton(page, 'dialog__confirm-btn')).toBeVisible();

    let deleteRequests = 0;
    page.on('request', (req) => {
      if (req.method() === 'DELETE' && req.url().includes('/api/subscriptions/')) {
        deleteRequests += 1;
      }
    });

    await dialogButton(page, 'dialog__confirm-btn').tap();

    await expect(page.locator('.sub-card')).toHaveCount(0);
    expect(deleteRequests).toBe(1);
  });

  test('編集モーダルからの削除も、キャンセルを挟んだあとで保存が壊れない', async ({ page }) => {
    // 編集モーダルを開く → 削除 → キャンセル
    await page.locator('.sub-card [data-action="edit"]').first().tap();
    await page.locator('#subscription-modal').waitFor({ state: 'visible' });

    await page.locator('#modal-delete-btn').tap();
    await expect(dialogButton(page, 'dialog__cancel-btn')).toBeVisible();
    await dialogButton(page, 'dialog__cancel-btn').tap();
    await expect(dialogButton(page, 'dialog__cancel-btn')).toBeHidden();

    // 修正前はここで保存ボタンがリスナーを失って無反応になっていた。
    // 値を変えて保存し、実際に反映されることを確認する。
    await page.locator('#service-name').fill('Disney Plus');
    await page.locator('#modal-save-btn').tap();
    await page.locator('#subscription-modal').waitFor({ state: 'hidden' });

    await expect(page.locator('.sub-card__name')).toHaveText('Disney Plus');
  });

  test('確認ダイアログのボタンは Shadow DOM 内でもタップ領域を満たす', async ({ page }) => {
    await page.locator('.sub-card [data-action="delete"]').first().tap();
    await expect(dialogButton(page, 'dialog__confirm-btn')).toBeVisible();

    // document.querySelectorAll は Shadow DOM を貫通しないため、
    // 専用のヘルパーで shadowRoot を再帰的に辿って検査する。
    // 範囲を ff-confirm-dialog に絞るのは、ページ全体の掃引は
    // tap-target-size.spec.js の担当だから（重複させない）。
    const issues = await tapTargetIssuesDeep(page, 'ff-confirm-dialog');
    expect(
      issues,
      `タップ領域が ${MIN_TAP_TARGET_PX}px 未満の要素:\n` +
        issues.map((i) => `  ${i.label} (${i.width}x${i.height})`).join('\n')
    ).toEqual([]);
  });
});
