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

        // Same reasoning as helpers/hit-test.js (duplicated here because
        // page.evaluate() serializes only the exact function passed in --
        // it cannot close over code from another module):
        //  - `aria-hidden="true"` marks an element as intentionally
        //    non-interactive (e.g. the visually-tiny native
        //    `<input type="file">` in csv-import-page.js, whose real tap
        //    target is the visible "ファイルを選択" button next to it).
        //  - While a true modal dialog is open, only its own controls are
        //    reachable; background controls (e.g. the hamburger button)
        //    are intentionally inert until the modal closes.
        function hasAriaHiddenSelfOrAncestor(el) {
          let node = el;
          while (node && node.nodeType === 1) {
            if (node.getAttribute && node.getAttribute('aria-hidden') === 'true') return true;
            node = node.parentElement;
          }
          return false;
        }

        const openModal = Array.from(
          document.querySelectorAll('[aria-modal="true"], [role="dialog"]')
        ).find(isRenderedVisible);
        const searchRoot = openModal || document;

        const results = [];
        const elements = Array.from(searchRoot.querySelectorAll(selector))
          .filter(isRenderedVisible)
          .filter((el) => !hasAriaHiddenSelfOrAncestor(el));

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

          let effectiveRect = rect;

          // A checkbox/radio's *own* box is often small by native
          // rendering, but if it's associated with a <label> (wrapping it,
          // or via for=id) that is itself large enough, the real tap
          // target a user interacts with is the whole label (native
          // browser behavior: clicking anywhere on the label toggles the
          // control).
          if (
            (rect.width < minSize || rect.height < minSize) &&
            el.tagName === 'INPUT' &&
            (el.type === 'checkbox' || el.type === 'radio')
          ) {
            const wrappingLabel = el.closest('label');
            const forLabel = el.id
              ? document.querySelector(`label[for="${window.CSS.escape(el.id)}"]`)
              : null;
            const label = wrappingLabel || forLabel;
            if (label) {
              const labelRect = label.getBoundingClientRect();
              if (labelRect.width >= minSize && labelRect.height >= minSize) {
                effectiveRect = labelRect;
              }
            }
          }

          if (effectiveRect.width < minSize || effectiveRect.height < minSize) {
            results.push({
              element: describe(el),
              width: Math.round(effectiveRect.width * 10) / 10,
              height: Math.round(effectiveRect.height * 10) / 10,
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
