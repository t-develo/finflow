/**
 * screens.js - Canonical list of screens the mobile-touch suite sweeps
 * across (hit-test regression, tap-target size, console/layout checks).
 *
 * NOTE on scope: `/expenses/new` and `/expenses/:id/edit` exist as a page
 * component (js/pages/expense-form-page.js) but are NOT registered as
 * routes in js/app.js, so navigating to them currently renders the SPA's
 * generic 404 view. That is a pre-existing routing gap unrelated to the
 * mobile-overlay bug this suite targets, and outside this agent's file
 * ownership (src/frontend is owned by another in-flight change), so those
 * routes are intentionally excluded from the sweep below rather than
 * silently asserted against a 404 page.
 */

'use strict';

const { registerNewUser, loginViaUi } = require('./auth');

/**
 * @typedef {Object} ScreenDef
 * @property {string} name - Japanese, human-readable screen name (used in
 *   test titles and failure messages).
 * @property {string} path - Route path to navigate to.
 * @property {boolean} requiresAuth - Whether a logged-in session is needed.
 * @property {(page: import('@playwright/test').Page) => Promise<void>} [setup]
 *   Optional extra interaction to reach the screen's state under test
 *   (e.g. opening a modal).
 */

/** @returns {ScreenDef[]} */
function buildScreens() {
  return [
    { name: 'ログイン画面', path: '/login', requiresAuth: false },
    { name: '新規登録画面', path: '/register', requiresAuth: false },
    { name: 'ダッシュボード画面', path: '/dashboard', requiresAuth: true },
    { name: '支出一覧画面', path: '/expenses', requiresAuth: true },
    { name: 'サブスクリプション一覧画面', path: '/subscriptions', requiresAuth: true },
    {
      name: 'サブスクリプション追加モーダル（開いた状態）',
      path: '/subscriptions',
      requiresAuth: true,
      setup: async (page) => {
        await page.locator('#add-subscription-btn').tap();
        await page.locator('#subscription-modal').waitFor({ state: 'visible' });
      },
    },
    { name: 'カテゴリ管理画面', path: '/categories', requiresAuth: true },
    { name: 'CSV取込画面', path: '/expenses/import', requiresAuth: true },
  ];
}

/**
 * Navigates to a screen, logging in with a fresh user first if required,
 * and waits for the screen's async data loads to settle.
 *
 * @param {import('@playwright/test').Page} page
 * @param {import('@playwright/test').APIRequestContext} request
 * @param {string} baseURL
 * @param {ScreenDef} screen
 */
async function gotoScreen(page, request, baseURL, screen) {
  if (screen.requiresAuth) {
    const { email, password } = await registerNewUser(request, baseURL);
    await loginViaUi(page, email, password);
  }

  await page.goto(screen.path);

  // Best-effort settle: most pages fetch data on load and swap out a
  // ".loading" placeholder. We don't fail the navigation if this never
  // fully reaches "networkidle" (e.g. the Chart.js CDN <script> tag may be
  // unreachable in a sandboxed environment; the dashboard already has a
  // graceful fallback for that case).
  await page.waitForLoadState('networkidle', { timeout: 5_000 }).catch(() => {});
  await page
    .locator('.loading')
    .first()
    .waitFor({ state: 'detached', timeout: 5_000 })
    .catch(() => {});

  if (screen.setup) {
    await screen.setup(page);
  }
}

module.exports = { buildScreens, gotoScreen };
