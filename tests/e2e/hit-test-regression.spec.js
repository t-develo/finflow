/**
 * hit-test-regression.spec.js
 *
 * The single most important guard in this suite: for every visible
 * interactive element on every screen, verify that a tap at its visual
 * center actually reaches that element (or a legitimate descendant/
 * ancestor/label), and not some unrelated element such as a transparent
 * full-screen overlay.
 *
 * This directly targets the reported root cause: `.sidebar-overlay` is
 * `position:fixed; inset:0; z-index:90; opacity:0` and gets `display:block`
 * under `@media (max-width:768px)` regardless of its "visible" modifier
 * class, so on mobile viewports it sits on top of the entire page and
 * swallows every tap even though it's invisible.
 *
 * Expected result BEFORE the CSS fix lands (pointer-events:none by default,
 * pointer-events:auto only when `--visible`): this test FAILS on every
 * mobile screen, because the overlay covers all elements. AFTER the fix,
 * it must PASS. Do not weaken these assertions to make it pass early.
 */

'use strict';

const { test, expect } = require('@playwright/test');
const { buildScreens, gotoScreen } = require('./helpers/screens');
const { findHitTestFailures, formatHitTestFailures } = require('./helpers/hit-test');

for (const screen of buildScreens()) {
  test(`ヒットテスト回帰ガード: ${screen.name}`, async ({ page, request, baseURL }) => {
    await gotoScreen(page, request, baseURL, screen);

    const failures = await findHitTestFailures(page);

    expect(failures, formatHitTestFailures(screen.name, failures)).toEqual([]);
  });
}
