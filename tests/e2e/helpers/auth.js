/**
 * auth.js - E2E helper: creates isolated test users and logs in through the
 * real UI (never by injecting localStorage), so that every authenticated
 * test also re-exercises the login tap flow end-to-end.
 *
 * Design goals (see CLAUDE.md / rules):
 *  - Independence: every helper call creates a brand-new user, so tests never
 *    share state or depend on execution order.
 *  - Certainty: we wait for actual state changes (URL change), never sleep().
 *  - Touch-first: all interactions use locator.tap(), not click(), because
 *    the bug under test (invisible full-screen overlay swallowing taps) is
 *    only reproducible through real touch/tap dispatch.
 */

'use strict';

const DEFAULT_PASSWORD = 'TestPass123'; // 8+ chars, includes a digit (Identity policy)

/**
 * Registers a brand-new user via the API (fast, deterministic) and returns
 * the credentials so the caller can log in through the UI.
 *
 * @param {import('@playwright/test').APIRequestContext} request
 * @param {string} baseURL
 * @returns {Promise<{ email: string, password: string }>}
 */
async function registerNewUser(request, baseURL) {
  const uniqueId = `${Date.now()}-${Math.floor(Math.random() * 1_000_000)}`;
  const email = `e2e-mobile-${uniqueId}@example.test`;
  const password = DEFAULT_PASSWORD;

  const response = await request.post(`${baseURL}/api/auth/register`, {
    data: { email, password },
  });

  if (!response.ok()) {
    const body = await response.text().catch(() => '<no body>');
    throw new Error(
      `[auth helper] POST /api/auth/register が失敗しました ` +
        `(status=${response.status()}): ${body}`
    );
  }

  return { email, password };
}

/**
 * Logs in through the real /login UI using tap() on every interactive
 * element (email field, password field, submit button), matching how a
 * mobile user would actually interact with the page.
 *
 * @param {import('@playwright/test').Page} page
 * @param {string} email
 * @param {string} password
 */
async function loginViaUi(page, email, password) {
  await page.goto('/login');

  const emailInput = page.locator('#login-email');
  const passwordInput = page.locator('#login-password');
  const submitBtn = page.locator('#login-submit-btn');

  await emailInput.waitFor({ state: 'visible' });

  // Tap first (this is the exact interaction the mobile bug breaks), then
  // fill the value programmatically for speed/determinism.
  await emailInput.tap();
  await emailInput.fill(email);

  await passwordInput.tap();
  await passwordInput.fill(password);

  await submitBtn.tap();

  // Wait for the SPA router to leave /login (pushState, not a full reload).
  await page.waitForFunction(
    () => window.location.pathname !== '/login',
    null,
    { timeout: 15_000 }
  );
}

/**
 * Convenience: register + log in via UI in one call.
 *
 * @param {import('@playwright/test').Page} page
 * @param {import('@playwright/test').APIRequestContext} request
 * @param {string} baseURL
 * @returns {Promise<{ email: string, password: string }>}
 */
async function registerAndLoginViaUi(page, request, baseURL) {
  const credentials = await registerNewUser(request, baseURL);
  await loginViaUi(page, credentials.email, credentials.password);
  return credentials;
}

module.exports = {
  DEFAULT_PASSWORD,
  registerNewUser,
  loginViaUi,
  registerAndLoginViaUi,
};
