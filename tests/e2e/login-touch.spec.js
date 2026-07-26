/**
 * login-touch.spec.js
 *
 * Direct regression tests for the three reported mobile symptoms, focused
 * on the /login screen: tapping the email/password fields should focus
 * them (proxy for "the on-screen keyboard would appear"), and tapping the
 * "new registration" link should navigate to /register.
 *
 * All interactions use locator.tap() (never click()), since click() does
 * not reproduce the invisible-overlay bug reliably.
 */

'use strict';

const { test, expect } = require('@playwright/test');

test.describe('ログイン画面: タップ操作', () => {
  test('ログイン画面: メールアドレス欄をタップするとフォーカスが入る', async ({ page }) => {
    await page.goto('/login');

    const emailInput = page.locator('#login-email');
    await emailInput.waitFor({ state: 'visible' });

    await emailInput.tap();

    await expect(emailInput).toBeFocused();

    const activeElementId = await page.evaluate(() => document.activeElement?.id ?? null);
    expect(
      activeElementId,
      'タップ後、document.activeElement が #login-email になっていません。' +
        '透明なオーバーレイ等が入力欄を覆っている可能性があります。'
    ).toBe('login-email');
  });

  test('ログイン画面: パスワード欄をタップするとフォーカスが入る', async ({ page }) => {
    await page.goto('/login');

    const passwordInput = page.locator('#login-password');
    await passwordInput.waitFor({ state: 'visible' });

    await passwordInput.tap();

    await expect(passwordInput).toBeFocused();

    const activeElementId = await page.evaluate(() => document.activeElement?.id ?? null);
    expect(
      activeElementId,
      'タップ後、document.activeElement が #login-password になっていません。' +
        '透明なオーバーレイ等が入力欄を覆っている可能性があります。'
    ).toBe('login-password');
  });

  test('ログイン画面: 「新規登録」リンクをタップすると /register に遷移する', async ({ page }) => {
    await page.goto('/login');

    const registerLink = page.locator('a[href="/register"][data-navigo]');
    await registerLink.waitFor({ state: 'visible' });

    await registerLink.tap();

    await page.waitForFunction(() => window.location.pathname === '/register', null, {
      timeout: 10_000,
    });

    await expect(page).toHaveURL(/\/register$/);
    await expect(page.locator('#register-form')).toBeVisible();
  });

  test('ログイン画面から新規登録画面へ遷移後、フォーム欄がタップでフォーカスできる', async ({
    page,
  }) => {
    await page.goto('/login');
    await page.locator('a[href="/register"][data-navigo]').tap();
    await page.waitForFunction(() => window.location.pathname === '/register', null, {
      timeout: 10_000,
    });

    const nameInput = page.locator('#reg-name');
    await nameInput.waitFor({ state: 'visible' });
    await nameInput.tap();

    await expect(nameInput).toBeFocused();
  });
});
