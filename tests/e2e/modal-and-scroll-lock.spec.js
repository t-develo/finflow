/**
 * modal-and-scroll-lock.spec.js
 *
 * Permanent regression guards for two "tap stops working" bugs found during
 * code review of the sidebar-overlay hit-test fix. Both were already fixed
 * in this branch, but neither had a durable assertion protecting the fix,
 * so a future refactor could silently reintroduce either without any test
 * noticing:
 *
 *  H1: components.css's `.modal__dialog` used to redeclare `max-height` at
 *      the mobile breakpoint (loaded after main.css, so it won the
 *      cascade), cancelling out main.css's `min-height: 100dvh` /
 *      `max-height: 100dvh` pairing. Because `.modal` (the fixed,
 *      inset:0 backdrop) never declares `overflow`, an unconstrained
 *      `.modal__dialog` is free to grow taller than the viewport when its
 *      content (form fields, etc.) needs more room than the viewport
 *      offers -- which pushes `.modal__footer` (and its Save/Cancel/Delete
 *      buttons) below the bottom of the viewport, where it is never
 *      reachable by a real tap. The current form is short enough to fit an
 *      ordinary phone-sized viewport even when unconstrained, so this only
 *      reproduces at a reduced viewport height (e.g. a landscape phone or a
 *      small device) -- which is exactly why this test deliberately shrinks
 *      the viewport rather than trusting the sweep in
 *      hit-test-regression.spec.js at default mobile sizes.
 *
 *      Note deliberately NOT delegated to helpers/hit-test.js's
 *      findHitTestFailures() alone: that sweep intentionally *skips*
 *      (rather than fails) any candidate element that doesn't intersect
 *      the viewport at all (see its own comments about off-canvas drawer
 *      links) -- exactly the failure mode H1 produces once the footer is
 *      pushed completely below the fold. So the primary guard here is an
 *      explicit `boundingBox()` containment check; findHitTestFailures() is
 *      reused as a secondary check for the "on screen but covered by
 *      something else" failure mode.
 *
 *  H2: js/app.js's openSidebar()/closeSidebar() toggle a
 *      `body--drawer-open` class on <body> to lock background scroll while
 *      the mobile drawer is open, but no CSS rule for that class existed
 *      anywhere, so the class was applied/removed with zero visible
 *      effect and the page behind the drawer kept scrolling. Checking only
 *      "the class is present" would not have caught this (the class *was*
 *      being applied correctly; only the corresponding CSS was missing),
 *      so this test also drives a real scroll attempt via
 *      `page.mouse.wheel()` and asserts `window.scrollY` doesn't move.
 */

'use strict';

const { test, expect } = require('@playwright/test');
const { subscriptionAddButton } = require('./helpers/subscriptions');
const { registerAndLoginViaUi } = require('./helpers/auth');
const { findHitTestFailures, formatHitTestFailures } = require('./helpers/hit-test');

// ---------------------------------------------------------------------------
// H1: subscription modal footer must stay fully inside the viewport, even
// at a reduced viewport height, and its buttons must be genuinely tappable.
// ---------------------------------------------------------------------------

test('サブスクモーダル: 画面高が低くてもフッターの操作ボタンが必ずビューポート内に収まりタップ可能である', async ({
  page,
  request,
  baseURL,
}) => {
  await registerAndLoginViaUi(page, request, baseURL);

  await page.goto('/subscriptions');
  await page.waitForLoadState('networkidle', { timeout: 5_000 }).catch(() => {});

  await subscriptionAddButton(page).tap();
  const modal = page.locator('#subscription-modal');
  await modal.waitFor({ state: 'visible' });

  const footerButtonIds = ['#modal-close-btn', '#modal-cancel-btn', '#modal-save-btn'];

  /**
   * Asserts every footer button is (a) fully within the current viewport
   * bounding box and (b) not covered by any other element at its visual
   * center point.
   * @param {string} label - used only in failure messages
   */
  async function assertFooterButtonsAreUsable(label) {
    const viewport = page.viewportSize();
    expect(viewport, `[${label}] viewportSize() が取得できませんでした`).not.toBeNull();

    for (const id of footerButtonIds) {
      const box = await page.locator(id).boundingBox();
      expect(box, `[${label}] ${id} の boundingBox が取得できませんでした（非表示?）`).not.toBeNull();

      const withinViewportMessage =
        `[${label}] ${id} がビューポート外にはみ出しています: ` +
        `box=${JSON.stringify(box)}, viewport=${JSON.stringify(viewport)}`;

      expect(box.y, withinViewportMessage).toBeGreaterThanOrEqual(0);
      expect(box.x, withinViewportMessage).toBeGreaterThanOrEqual(0);
      expect(box.y + box.height, withinViewportMessage).toBeLessThanOrEqual(viewport.height);
      expect(box.x + box.width, withinViewportMessage).toBeLessThanOrEqual(viewport.width);
    }

    // Secondary guard, reusing the shared hit-test helper: while the modal
    // is open, findHitTestFailures() restricts its sweep to the modal's own
    // controls (see its `openModal` search-root logic) and reports any
    // visible-but-covered element. It intentionally does NOT flag elements
    // that don't intersect the viewport at all (that failure mode is caught
    // by the boundingBox() assertions above instead).
    const failures = await findHitTestFailures(page);
    expect(failures, formatHitTestFailures(`サブスクモーダル (${label})`, failures)).toEqual([]);
  }

  await test.step('通常のモバイル高さでは既にフッターが収まっている（前提の健全性確認）', async () => {
    await assertFooterButtonsAreUsable('通常のビューポート高さ');
  });

  await test.step('画面高を大幅に下げても max-height の制約により footer はビューポート内に留まる', async () => {
    // A short, landscape-phone-ish height. The current form's natural
    // content height comfortably exceeds this, so if `.modal__dialog` were
    // ever left unconstrained again (H1's root cause), the dialog would
    // grow taller than this viewport and push the footer below the fold.
    await page.setViewportSize({ width: 390, height: 480 });

    await assertFooterButtonsAreUsable('低いビューポート高さ (390x480)');

    // Also confirm each button is really tappable end-to-end, not just
    // geometrically present: a real tap must actually reach it.
    await page.locator('#modal-cancel-btn').tap();
    await expect(modal).toBeHidden();
  });
});

// ---------------------------------------------------------------------------
// H2: opening the mobile drawer must actually prevent the page behind it
// from scrolling, and closing it must restore normal scrolling.
// ---------------------------------------------------------------------------

test('モバイルドロワー展開中は背面ページが実際にスクロールしない（閉じると復帰する）', async ({
  page,
  request,
  baseURL,
}) => {
  await registerAndLoginViaUi(page, request, baseURL);

  // registerAndLoginViaUi lands on `/` (dashboard) after a successful login.
  await page.waitForLoadState('networkidle', { timeout: 5_000 }).catch(() => {});
  await page
    .locator('.loading')
    .first()
    .waitFor({ state: 'detached', timeout: 5_000 })
    .catch(() => {});

  // The dashboard's real content height can vary (e.g. Chart.js may fail to
  // load from the CDN in a sandboxed environment and fall back to a shorter
  // list view), so don't rely on it for scrollability. Append a tall filler
  // element so this test deterministically exercises the actual
  // scroll-locking CSS/JS rather than depending on incidental page length.
  await page.evaluate(() => {
    const filler = document.createElement('div');
    filler.id = 'e2e-scroll-lock-filler';
    filler.style.cssText = 'height:3000px;width:1px;';
    document.body.appendChild(filler);
  });

  // --- Sanity check: confirm the page is genuinely scrollable *before* the
  // drawer opens (otherwise a "scrollY didn't change" assertion later would
  // be trivially true for the wrong reason).
  await page.mouse.wheel(0, 500);
  await page.waitForFunction(() => window.scrollY > 0, null, { timeout: 5_000 });
  const scrollYBeforeDrawer = await page.evaluate(() => window.scrollY);
  expect(scrollYBeforeDrawer, '前提確認: ドロワーを開く前はスクロールできるはずです').toBeGreaterThan(0);

  await page.evaluate(() => window.scrollTo(0, 0));
  await page.waitForFunction(() => window.scrollY === 0);

  // --- Open the drawer.
  const hamburgerBtn = page.locator('#hamburger-btn');
  await hamburgerBtn.waitFor({ state: 'visible' });
  await hamburgerBtn.tap();

  await expect(page.locator('body')).toHaveClass(/body--drawer-open/);

  const overflowWhileOpen = await page.evaluate(
    () => getComputedStyle(document.body).overflow
  );
  expect(
    overflowWhileOpen,
    'ドロワー展開中は body の overflow が hidden になっているはずです（body--drawer-open のCSSが有効か確認）'
  ).toBe('hidden');

  // --- Actually try to scroll while the drawer is open: scrollY must not
  // move at all, not merely "the class is present".
  await page.mouse.wheel(0, 500);
  const scrollYWhileOpen = await page.evaluate(() => window.scrollY);
  expect(
    scrollYWhileOpen,
    `ドロワー展開中にスクロールが発生しました (scrollY=${scrollYWhileOpen})。` +
      '背面スクロール抑止が効いていません。'
  ).toBe(0);

  // --- Close the drawer: everything must be restored.
  await hamburgerBtn.tap();
  await expect(page.locator('body')).not.toHaveClass(/body--drawer-open/);

  const overflowAfterClose = await page.evaluate(
    () => getComputedStyle(document.body).overflow
  );
  expect(
    overflowAfterClose,
    'ドロワーを閉じた後は body の overflow が元に戻っているはずです'
  ).not.toBe('hidden');

  await page.mouse.wheel(0, 500);
  await page.waitForFunction(() => window.scrollY > 0, null, { timeout: 5_000 });
  const scrollYAfterClose = await page.evaluate(() => window.scrollY);
  expect(
    scrollYAfterClose,
    'ドロワーを閉じた後は再びスクロールできるはずです'
  ).toBeGreaterThan(0);
});
