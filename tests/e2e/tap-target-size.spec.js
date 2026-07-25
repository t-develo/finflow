/**
 * tap-target-size.spec.js
 *
 * Sweeps every screen and asserts that every visible interactive element
 * (button, a[href], input, select, textarea) has a bounding box of at
 * least 44x44 CSS pixels -- the widely used minimum comfortable touch
 * target size (WCAG 2.5.5 / Apple HIG / Material Design all converge
 * around this number).
 *
 * Intentional exceptions go in TAP_TARGET_ALLOWLIST below, each with a
 * comment explaining *why* it's acceptable to be smaller. Do not add
 * entries here just to make a failing test pass -- only add an entry when
 * there is a genuine, reviewed UX reason (e.g. a purely decorative control
 * with a large enough effective/parent hit area).
 */

'use strict';

const { test, expect } = require('@playwright/test');
const { buildScreens, gotoScreen } = require('./helpers/screens');
const { INTERACTIVE_SELECTOR } = require('./helpers/hit-test');

const MIN_TAP_TARGET_PX = 44;

/**
 * Allowlist of selectors that are permitted to be smaller than
 * MIN_TAP_TARGET_PX. Empty by default: no exceptions have been reviewed
 * and approved yet. Keys are CSS selectors matched against the element;
 * values are the justification.
 *
 * @type {Record<string, string>}
 */
const TAP_TARGET_ALLOWLIST = {
  // Example (do not uncomment without a real, reviewed justification):
  // '#some-decorative-icon-link': 'Purely decorative; the actual tap
  //   target is the parent <button> which is >= 44x44px.',
};

for (const screen of buildScreens()) {
  test(`タップ領域サイズ (44x44px 以上): ${screen.name}`, async ({ page, request, baseURL }) => {
    await gotoScreen(page, request, baseURL, screen);

    const undersized = await page.evaluate(
      ({ selector, minSize, allowlistSelectors }) => {
        function describe(el) {
          const id = el.id ? `#${el.id}` : '';
          const classAttr =
            typeof el.className === 'string' && el.className.trim()
              ? `.${el.className.trim().split(/\s+/).join('.')}`
              : '';
          const text = (el.textContent || '').trim().replace(/\s+/g, ' ').slice(0, 40);
          return `<${el.tagName.toLowerCase()}${id}${classAttr}>${text ? ` "${text}"` : ''}`;
        }

        function isRenderedVisible(el) {
          const style = window.getComputedStyle(el);
          if (style.display === 'none') return false;
          if (style.visibility === 'hidden' || style.visibility === 'collapse') return false;
          if (Number(style.opacity) === 0) return false;
          const rect = el.getBoundingClientRect();
          return rect.width > 0 && rect.height > 0;
        }

        const results = [];
        const elements = Array.from(document.querySelectorAll(selector)).filter(
          isRenderedVisible
        );

        for (const el of elements) {
          const isAllowlisted = allowlistSelectors.some((sel) => {
            try {
              return el.matches(sel);
            } catch {
              return false;
            }
          });
          if (isAllowlisted) continue;

          // Elements moved fully off-canvas (e.g. a closed mobile drawer's
          // links, translated out of view with `translateX(-100%)`) are
          // not currently tappable at all, so their size is not currently
          // relevant -- skip rather than flag as undersized.
          el.scrollIntoView({ block: 'center', inline: 'center', behavior: 'instant' });
          const rect = el.getBoundingClientRect();
          const intersectsViewport =
            rect.right > 0 &&
            rect.bottom > 0 &&
            rect.left < window.innerWidth &&
            rect.top < window.innerHeight;
          if (!intersectsViewport) continue;

          if (rect.width < minSize || rect.height < minSize) {
            results.push({
              element: describe(el),
              width: Math.round(rect.width * 10) / 10,
              height: Math.round(rect.height * 10) / 10,
            });
          }
        }

        return results;
      },
      {
        selector: INTERACTIVE_SELECTOR,
        minSize: MIN_TAP_TARGET_PX,
        allowlistSelectors: Object.keys(TAP_TARGET_ALLOWLIST),
      }
    );

    const message =
      undersized.length > 0
        ? `[${screen.name}] ${undersized.length}件の要素が${MIN_TAP_TARGET_PX}x${MIN_TAP_TARGET_PX}px 未満です:\n` +
          undersized
            .map((u) => `  - ${u.element}: ${u.width}x${u.height}px`)
            .join('\n')
        : '';

    expect(undersized, message).toEqual([]);
  });
}
