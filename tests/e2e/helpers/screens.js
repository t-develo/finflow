/**
 * screens.js - Canonical list of screens the mobile-touch suite sweeps
 * across (hit-test regression, tap-target size, console/layout checks).
 *
 * `/expenses/new` and `/expenses/:id/edit` ARE registered routes in
 * js/app.js (see the `.on('/expenses/new', ...)` and
 * `.on('/expenses/:id/edit', ...)` handlers there) and render the real
 * expense-form-page.js component, so both are included in the sweep below.
 * The edit screen needs a real expense id to navigate to; `resolvePath()`
 * creates one through the authenticated API (see `createExpenseForEdit`)
 * using the same test user that `gotoScreen` just logged in as.
 */

'use strict';

const { registerNewUser, loginViaUi } = require('./auth');

/**
 * Logs in via the real API (not the UI helper) to obtain a bearer token,
 * then creates a category (if the new user has none yet) and an expense
 * through the authenticated API, so the E2E suite can navigate straight to
 * `/expenses/:id/edit` for a real, owned record.
 *
 * This intentionally duplicates a login call (in addition to the UI login
 * `gotoScreen` already performed with the same credentials) rather than
 * reaching into `helpers/auth.js` for the token: `registerNewUser()` there
 * only returns `{ email, password }`, and screens.js does not own that
 * file, so it fetches its own token via `POST /api/auth/login` instead of
 * changing auth.js's return shape.
 *
 * @param {import('@playwright/test').APIRequestContext} request
 * @param {string} baseURL
 * @param {{ email: string, password: string }} credentials
 * @returns {Promise<number>} the created expense's id
 */
async function createExpenseForEdit(request, baseURL, credentials) {
  const loginResponse = await request.post(`${baseURL}/api/auth/login`, {
    data: { email: credentials.email, password: credentials.password },
  });
  if (!loginResponse.ok()) {
    throw new Error(
      `[screens helper] POST /api/auth/login が失敗しました (status=${loginResponse.status()})`
    );
  }
  const { token } = await loginResponse.json();
  const authHeaders = { Authorization: `Bearer ${token}` };

  let categories = await request
    .get(`${baseURL}/api/categories`, { headers: authHeaders })
    .then((res) => res.json());

  let categoryId = categories[0]?.id;
  if (!categoryId) {
    const createCategoryResponse = await request.post(`${baseURL}/api/categories`, {
      headers: authHeaders,
      data: { name: 'E2Eテスト用カテゴリ', color: '#3B82F6' },
    });
    if (!createCategoryResponse.ok()) {
      throw new Error(
        `[screens helper] POST /api/categories が失敗しました (status=${createCategoryResponse.status()})`
      );
    }
    categoryId = (await createCategoryResponse.json()).id;
  }

  const today = new Date().toISOString().slice(0, 10);
  const createExpenseResponse = await request.post(`${baseURL}/api/expenses`, {
    headers: authHeaders,
    data: {
      amount: 1000,
      categoryId,
      date: today,
      description: 'E2E編集画面確認用の支出',
    },
  });
  if (!createExpenseResponse.ok()) {
    const body = await createExpenseResponse.text().catch(() => '<no body>');
    throw new Error(
      `[screens helper] POST /api/expenses が失敗しました (status=${createExpenseResponse.status()}): ${body}`
    );
  }
  const created = await createExpenseResponse.json();
  return created.id;
}

/**
 * @typedef {Object} ScreenDef
 * @property {string} name - Japanese, human-readable screen name (used in
 *   test titles and failure messages).
 * @property {string} [path] - Route path to navigate to. Omit when
 *   `resolvePath` is provided instead (for routes that need a real id).
 * @property {boolean} requiresAuth - Whether a logged-in session is needed.
 * @property {(ctx: { request: import('@playwright/test').APIRequestContext, baseURL: string, credentials: { email: string, password: string } | null }) => Promise<string>} [resolvePath]
 *   Optional async resolver used instead of the static `path` when the
 *   route needs data created ahead of time (e.g. a real expense id for
 *   `/expenses/:id/edit`). Receives the same `credentials` the screen was
 *   just logged in with via `registerNewUser`, so any records it creates
 *   through the API belong to the user the UI session is authenticated as.
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
    { name: '支出追加フォーム画面', path: '/expenses/new', requiresAuth: true },
    {
      name: '支出編集フォーム画面',
      requiresAuth: true,
      resolvePath: async ({ request, baseURL, credentials }) => {
        const expenseId = await createExpenseForEdit(request, baseURL, credentials);
        return `/expenses/${expenseId}/edit`;
      },
    },
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
  let credentials = null;
  if (screen.requiresAuth) {
    credentials = await registerNewUser(request, baseURL);
    await loginViaUi(page, credentials.email, credentials.password);
  }

  const path = screen.resolvePath
    ? await screen.resolvePath({ request, baseURL, credentials })
    : screen.path;

  await page.goto(path);

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
