/**
 * playwright.config.js - FinFlow mobile touch regression suite.
 *
 * Scope: this config/tests directory is a self-contained, test-only
 * toolchain. It does not add a build step to the app itself (per
 * CLAUDE.md's "no build tools" policy for the frontend) -- it only spawns
 * the existing `dotnet run` process and drives it with a browser.
 *
 * Environment constraints this file is written to respect:
 *  - Only a Chromium build is available at PLAYWRIGHT_BROWSERS_PATH
 *    (no WebKit/Firefox). Playwright's built-in `devices['iPhone *']`
 *    descriptors default to WebKit, so they are intentionally NOT used.
 *    Instead we use `devices['Pixel 7']` (Chromium-based) plus a
 *    hand-rolled iPhone-sized viewport that explicitly pins
 *    `defaultBrowserType: 'chromium'`.
 *  - `ASPNETCORE_URLS` is HTTP-only on purpose: Program.cs calls
 *    `app.UseHttpsRedirection()`, which becomes a real (307) redirect the
 *    moment an HTTPS endpoint is configured. Mixing https in here would
 *    break every request in this suite.
 */

'use strict';

const { defineConfig, devices } = require('@playwright/test');

const PORT = 5212;
const BASE_URL = `http://127.0.0.1:${PORT}`;

/**
 * Hand-rolled "iPhone-sized" mobile device descriptor running on Chromium.
 * Mirrors the viewport/DPI of a modern iPhone (e.g. iPhone 14) without
 * requiring WebKit, which is not installed in this environment.
 */
const IPHONE_SIZE_CHROMIUM_DEVICE = {
  viewport: { width: 390, height: 844 },
  deviceScaleFactor: 3,
  isMobile: true,
  hasTouch: true,
  defaultBrowserType: 'chromium',
  userAgent:
    'Mozilla/5.0 (Linux; Android 14; FinFlow-E2E iPhone-size emulation) ' +
    'AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.7390.37 Mobile Safari/537.36',
};

module.exports = defineConfig({
  testDir: './tests/e2e',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  // ローカル(このサンドボックス)では workers を 1 に固定する。
  // ここで起動する dev サーバーは `dotnet run` の Kestrel であり、実際に
  // 検証したところ、既定の worker 数(ローカルは CPU数まで並列)で複数の
  // Playwright ワーカーから同時にリクエストを浴びせると Kestrel が応答不能
  // になったりクラッシュしたりして `npx playwright test` 自体が不安定に
  // なることを確認済み。テストのアサーションを緩めるのではなく、実行条件
  // (並列度)を実際に安定して通る値に合わせるための変更。
  // CI 環境では専用のランナーリソースが確保され、このサンドボックスのような
  // 競合が起きにくいため、引き続き 2 workers でスケールさせる
  // （CIでも問題が再発するようであれば同様に 1 へ絞ることを検討）。
  workers: process.env.CI ? 2 : 1,
  timeout: 45_000,
  expect: { timeout: 10_000 },
  reporter: process.env.CI
    ? [['list'], ['html', { open: 'never' }]]
    : [['list']],

  use: {
    baseURL: BASE_URL,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'off',
    actionTimeout: 15_000,
    navigationTimeout: 30_000,
  },

  projects: [
    {
      // Primary mobile target: Chromium's own "Pixel 7" emulation profile.
      name: 'mobile-chrome-pixel7',
      use: { ...devices['Pixel 7'] },
    },
    {
      // Secondary mobile target: iPhone-sized viewport, still on Chromium
      // (WebKit is not available in this sandbox). Runs the same full
      // suite so the overlay-hit-testing bug is covered on both device
      // proportions.
      name: 'mobile-chrome-iphone-size',
      use: { ...IPHONE_SIZE_CHROMIUM_DEVICE },
    },
    {
      // Desktop regression guard: confirms the hit-test sweep and the
      // console/layout checks still pass on a normal desktop viewport.
      // `hasTouch: true` is added on top of the stock "Desktop Chrome"
      // device (which otherwise has no touch support) purely so the
      // shared login helper's locator.tap() calls work here too --
      // desktop viewport/scale are unaffected. Screens that require
      // login still exercise the exact same tap-based flow as the mobile
      // projects; only the mobile-only drawer/hamburger specs are
      // excluded here via testMatch.
      name: 'desktop-chromium-regression',
      use: { ...devices['Desktop Chrome'], hasTouch: true },
      testMatch: [
        '**/hit-test-regression.spec.js',
        '**/console-and-layout.spec.js',
      ],
    },
  ],

  webServer: {
    command: 'dotnet run --project src/FinFlow.Api',
    url: `${BASE_URL}/login`,
    reuseExistingServer: !process.env.CI,
    timeout: 180_000,
    env: {
      ASPNETCORE_URLS: BASE_URL, // HTTP only -- see file header note.
      ASPNETCORE_ENVIRONMENT: 'Development',
    },
  },
});
