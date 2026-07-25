/**
 * hidden-attribute.spec.js
 *
 * Verifies that the native `hidden` attribute actually hides elements.
 * The browser's UA stylesheet default (`[hidden] { display: none }`) has
 * low specificity and can silently be overridden by an author rule such as
 * `.modal { display: flex; }` or `.hamburger-btn { display: flex; }` inside
 * a media query -- in which case the element stays visible (and, worse,
 * still tappable) even though application code believes it is hidden.
 *
 * Another agent is adding a `[hidden] { display: none !important; }` guard
 * rule concurrently; these tests assert on the *effect* of that guard
 * (elements with `hidden` must be genuinely non-visible), not on the CSS
 * rule itself.
 */

'use strict';

const { test, expect } = require('@playwright/test');
const { registerAndLoginViaUi } = require('./helpers/auth');

test.describe('hidden 属性の実効性', () => {
  test('サブスク新規追加モーダル: 削除ボタンは新規作成時 hidden で不可視', async ({
    page,
    request,
    baseURL,
  }) => {
    await registerAndLoginViaUi(page, request, baseURL);
    await page.goto('/subscriptions');

    await page.locator('#add-subscription-btn').tap();
    await page.locator('#subscription-modal').waitFor({ state: 'visible' });

    const deleteBtn = page.locator('#modal-delete-btn');

    // Attribute-level assertion (application intent)...
    await expect(deleteBtn).toHaveAttribute('hidden', '');

    // ...and the actually-rendered assertion (what the CSS cascade does).
    // toBeHidden() checks computed visibility/display, not just the
    // attribute, so this is the check that would catch a broken
    // `[hidden]` guard.
    await expect(deleteBtn).toBeHidden();
  });

  test('サブスク編集モーダル: 削除ボタンは編集時に表示される', async ({
    page,
    request,
    baseURL,
  }) => {
    await registerAndLoginViaUi(page, request, baseURL);
    await page.goto('/subscriptions');

    // Create a subscription first so there is a row to edit.
    await page.locator('#add-subscription-btn').tap();
    await page.locator('#subscription-modal').waitFor({ state: 'visible' });

    await page.locator('#service-name').tap();
    await page.locator('#service-name').fill('E2Eテストサブスク');
    await page.locator('#amount').tap();
    await page.locator('#amount').fill('980');
    await page.locator('#next-billing-date').fill('2026-12-01');

    await page.locator('#modal-save-btn').tap();
    await page.locator('#subscription-modal').waitFor({ state: 'hidden' });

    const editBtn = page.locator('[data-action="edit"]').first();
    await editBtn.waitFor({ state: 'visible' });
    await editBtn.tap();

    await page.locator('#subscription-modal').waitFor({ state: 'visible' });

    const deleteBtn = page.locator('#modal-delete-btn');
    await expect(deleteBtn).not.toHaveAttribute('hidden', '');
    await expect(deleteBtn).toBeVisible();
  });

  test('ログイン画面: ハンバーガーボタンは hidden で不可視（モバイル）', async ({ page }) => {
    await page.goto('/login');

    const hamburgerBtn = page.locator('#hamburger-btn');

    await expect(hamburgerBtn).toHaveAttribute('hidden', '');
    await expect(hamburgerBtn).toBeHidden();
  });
});
