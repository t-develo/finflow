/**
 * subscriptions.js — サブスク画面の共通ロケータ。
 *
 * 「追加」の導線は画面幅で切り替わる:
 *   - モバイル (<=768px): 右下の FAB (#add-subscription-fab)
 *   - デスクトップ:        ヘッダーのボタン (#add-subscription-btn)
 * 同じ操作を 2 つ並べないため、片方は CSS で display:none にしている。
 *
 * どちらの構成でもテストが通るよう、**表示されている方**を掴むための
 * ヘルパーをここに集約する。各スペックが個別に ID を直書きすると、
 * 導線を変えるたびに複数ファイルが同時に壊れる。
 */

'use strict';

/** @param {import('@playwright/test').Page} page */
function subscriptionAddButton(page) {
  return page
    .locator('#add-subscription-fab, #add-subscription-btn')
    .filter({ visible: true })
    .first();
}

/**
 * 追加モーダル（モバイルではボトムシート）を開いて、表示されるまで待つ。
 * @param {import('@playwright/test').Page} page
 */
async function openSubscriptionModal(page) {
  await subscriptionAddButton(page).tap();
  await page.locator('#subscription-modal').waitFor({ state: 'visible' });
}

module.exports = { subscriptionAddButton, openSubscriptionModal };
