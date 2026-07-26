/**
 * sidebar-navigation.spec.js
 *
 * Regression tests for the mobile hamburger menu / sidebar / overlay flow
 * described in app.js:
 *   - Tapping #hamburger-btn toggles `sidebar--mobile-open` on #sidebar and
 *     `sidebar-overlay--visible` on #sidebar-overlay.
 *   - Tapping the overlay closes the sidebar.
 *   - Tapping a nav link inside the sidebar navigates AND closes the
 *     sidebar.
 *
 * Every test registers its own fresh user and logs in via the real UI, so
 * tests are independent and each one re-validates that login itself is
 * tappable (which is exactly the class of bug under test).
 */

'use strict';

const { test, expect } = require('@playwright/test');
const { registerAndLoginViaUi } = require('./helpers/auth');

test.describe('モバイルサイドバー: ハンバーガーメニュー操作', () => {
  test.beforeEach(async ({ page, request, baseURL }) => {
    await registerAndLoginViaUi(page, request, baseURL);
  });

  test('ハンバーガーボタンをタップするとサイドバーが開閉する', async ({ page }) => {
    const hamburgerBtn = page.locator('#hamburger-btn');
    const sidebar = page.locator('#sidebar');

    await hamburgerBtn.waitFor({ state: 'visible' });
    await expect(sidebar).not.toHaveClass(/sidebar--mobile-open/);

    // Open
    await hamburgerBtn.tap();
    await expect(sidebar).toHaveClass(/sidebar--mobile-open/);

    // Close (tap again)
    await hamburgerBtn.tap();
    await expect(sidebar).not.toHaveClass(/sidebar--mobile-open/);
  });

  test('サイドバーを開いた状態でオーバーレイをタップすると閉じる', async ({ page }) => {
    const hamburgerBtn = page.locator('#hamburger-btn');
    const sidebar = page.locator('#sidebar');
    const overlay = page.locator('#sidebar-overlay');

    await hamburgerBtn.waitFor({ state: 'visible' });
    await hamburgerBtn.tap();
    await expect(sidebar).toHaveClass(/sidebar--mobile-open/);
    await expect(overlay).toHaveClass(/sidebar-overlay--visible/);

    await overlay.tap();

    await expect(sidebar).not.toHaveClass(/sidebar--mobile-open/);
    await expect(overlay).not.toHaveClass(/sidebar-overlay--visible/);
  });

  test('サイドバー内のナビリンクをタップすると画面遷移し、サイドバーも閉じる', async ({
    page,
  }) => {
    const hamburgerBtn = page.locator('#hamburger-btn');
    const sidebar = page.locator('#sidebar');
    const expensesLink = page.locator('#sidebar .sidebar__link[href="/expenses"]');

    await hamburgerBtn.waitFor({ state: 'visible' });
    await hamburgerBtn.tap();
    await expect(sidebar).toHaveClass(/sidebar--mobile-open/);

    await expensesLink.waitFor({ state: 'visible' });
    await expensesLink.tap();

    await page.waitForFunction(() => window.location.pathname === '/expenses', null, {
      timeout: 10_000,
    });
    await expect(page).toHaveURL(/\/expenses$/);

    // Navigating via a sidebar link must also close the drawer.
    await expect(sidebar).not.toHaveClass(/sidebar--mobile-open/);
  });

  test('ハンバーガーボタンを2回タップして開閉を繰り返しても状態が一貫している', async ({
    page,
  }) => {
    const hamburgerBtn = page.locator('#hamburger-btn');
    const sidebar = page.locator('#sidebar');

    await hamburgerBtn.waitFor({ state: 'visible' });

    for (let i = 0; i < 2; i += 1) {
      await hamburgerBtn.tap();
      await expect(sidebar).toHaveClass(/sidebar--mobile-open/);

      await hamburgerBtn.tap();
      await expect(sidebar).not.toHaveClass(/sidebar--mobile-open/);
    }
  });
});
