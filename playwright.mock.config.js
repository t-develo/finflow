/**
 * playwright.mock.config.js — .NET 抜きでフロントエンドを検証するための構成。
 *
 * 既定の playwright.config.js は `dotnet run` で本物の API を起動する。
 * それが使える環境ではそちらを使うこと（本物の契約を検証できる唯一の構成）。
 *
 * この構成は、.NET SDK が無い環境（クラウド上の作業コンテナなど）でも
 * フロントエンドの回帰テストを回せるようにするためのもの。
 * `tests/e2e/mock-server/server.js` が src/frontend を静的配信しつつ
 * /api/* をインメモリで応答する。
 *
 * 対象は「サーバー実装に依存しない」スペックに限定する:
 *  - フロント側のリスナー/描画/タップ領域の回帰
 * 逆に、本物の API 契約や DB の検証はこの構成では**できない**。
 */

'use strict';

const { defineConfig, devices } = require('@playwright/test');

const PORT = Number(process.env.MOCK_PORT || 5299);
const BASE_URL = `http://127.0.0.1:${PORT}`;

/** WebKit が無い環境向けの、iPhone 相当ビューポート（Chromium ベース）。 */
const IPHONE_SIZE_CHROMIUM_DEVICE = {
  viewport: { width: 390, height: 844 },
  deviceScaleFactor: 3,
  isMobile: true,
  hasTouch: true,
  defaultBrowserType: 'chromium',
};

module.exports = defineConfig({
  testDir: './tests/e2e',
  fullyParallel: false,
  workers: 1,
  timeout: 45_000,
  expect: { timeout: 10_000 },
  reporter: [['list']],

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
      name: 'mock-mobile-chrome-pixel7',
      use: { ...devices['Pixel 7'] },
    },
    {
      name: 'mock-mobile-chrome-iphone-size',
      use: { ...IPHONE_SIZE_CHROMIUM_DEVICE },
    },
  ],

  webServer: {
    command: `node tests/e2e/mock-server/server.js ${PORT}`,
    url: `${BASE_URL}/login`,
    reuseExistingServer: true,
    timeout: 30_000,
  },
});
