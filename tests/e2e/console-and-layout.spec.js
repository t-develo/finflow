/**
 * console-and-layout.spec.js
 *
 * Two cross-cutting checks, swept across every screen:
 *   1. No JavaScript console errors / uncaught exceptions.
 *   2. No horizontal overflow (scrollWidth <= clientWidth) -- a classic
 *      symptom of the same class of mobile-layout bugs as the overlay
 *      issue (a fixed/absolute element wider than the viewport also often
 *      breaks tap targets, since it participates in hit-testing).
 *
 * Known, environment-specific exception: `dashboard-page.js` loads Chart.js
 * from a public CDN (cdn.jsdelivr.net) on demand, by injecting a <script>
 * tag the first time the dashboard renders. (It used to be a blocking
 * <script> in index.html's <head>, which froze the whole page until the
 * request timed out on offline/LAN-only deployments -- see
 * login-overlay-regression.spec.js for the guard against that regressing.)
 * In a network-sandboxed test environment that request can fail; the app
 * already handles a missing `window.Chart` gracefully (see
 * dashboard-page.js's `renderCategoryListFallback`), so a *resource load*
 * failure for that specific URL is filtered out here as an infrastructure
 * limitation, not an application bug. Any other console error still fails
 * the test.
 */

'use strict';

const { test, expect } = require('@playwright/test');
const { buildScreens, gotoScreen } = require('./helpers/screens');

const CDN_CHART_JS_PATTERN = /cdn\.jsdelivr\.net.*chart(\.umd)?\.min\.js/i;

// Chrome logs a *generic* "Failed to load resource: net::ERR_..." console
// message for failed <script>/<img>/etc. fetches; the failing URL is only
// available via the message's `location()`, not its text. So we check both
// the message text (covers messages that do embed a URL) and the location
// URL (covers the generic network-failure case actually seen in sandboxed
// environments without internet access to the CDN).
function isIgnorableExternalResourceError(text, locationUrl) {
  return CDN_CHART_JS_PATTERN.test(text) || CDN_CHART_JS_PATTERN.test(locationUrl || '');
}

for (const screen of buildScreens()) {
  test(`コンソールエラー無し・横スクロール無し: ${screen.name}`, async ({
    page,
    request,
    baseURL,
  }) => {
    /** @type {string[]} */
    const consoleErrors = [];
    /** @type {string[]} */
    const pageErrors = [];

    page.on('console', (msg) => {
      if (
        msg.type() === 'error' &&
        !isIgnorableExternalResourceError(msg.text(), msg.location()?.url)
      ) {
        consoleErrors.push(msg.text());
      }
    });

    page.on('pageerror', (err) => {
      if (!isIgnorableExternalResourceError(err.message)) {
        pageErrors.push(err.message);
      }
    });

    await gotoScreen(page, request, baseURL, screen);

    // --- 1. No console errors / uncaught exceptions ---
    const allErrors = [...consoleErrors, ...pageErrors];
    expect(
      allErrors,
      `[${screen.name}] コンソールエラーが検出されました:\n` +
        allErrors.map((e, i) => `  ${i + 1}. ${e}`).join('\n')
    ).toEqual([]);

    // --- 2. No horizontal overflow ---
    const overflow = await page.evaluate(() => {
      const root = document.documentElement;
      return {
        scrollWidth: root.scrollWidth,
        clientWidth: root.clientWidth,
      };
    });

    expect(
      overflow.scrollWidth,
      `[${screen.name}] 横スクロールが発生しています ` +
        `(document.documentElement.scrollWidth=${overflow.scrollWidth} > ` +
        `clientWidth=${overflow.clientWidth}). 画面幅を超える固定/絶対配置要素がある可能性があります。`
    ).toBeLessThanOrEqual(overflow.clientWidth);
  });
}
