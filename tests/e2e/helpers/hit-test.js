/**
 * hit-test.js - Core "is this tappable element actually hittable?" check.
 *
 * This is the regression guard for the root-cause bug described in the task:
 * a transparent full-screen overlay (`.sidebar-overlay`, opacity:0 but still
 * receiving pointer events) sitting on top of everything on mobile viewports
 * and swallowing every tap.
 *
 * Strategy: for every *visually rendered* interactive element (button, link,
 * input, select, textarea, [role="button"]) on the page, compute its visual
 * center point and ask the browser (via document.elementFromPoint) which
 * element actually receives a tap/click there. If that's neither the target
 * itself, nor a descendant of the target (e.g. an icon inside a button), nor
 * an ancestor of the target (e.g. a wrapping <label>), the target is
 * considered "blocked" and reported as a failure.
 *
 * Deliberately NOT used as a visibility signal: the `hidden` attribute.
 * Whether `hidden` actually hides an element depends on the CSS cascade
 * (an author rule can override the UA default `[hidden]{display:none}`
 * unless a `!important` guard is present) -- that is itself one of the bugs
 * this suite must be able to catch. Visibility is therefore always derived
 * from computed style, never from the attribute.
 */

'use strict';

const INTERACTIVE_SELECTOR = [
  'button',
  'a[href]',
  'input',
  'select',
  'textarea',
  '[role="button"]',
].join(',');

/**
 * Runs the hit-test inside the page and returns an array of failure
 * descriptors (empty array = all good).
 *
 * @param {import('@playwright/test').Page} page
 * @returns {Promise<Array<{
 *   target: string, targetSelector: string,
 *   coveredBy: string, coveredBySelector: string|null,
 *   x: number, y: number
 * }>>}
 */
async function findHitTestFailures(page) {
  return page.evaluate((selector) => {
    /** @param {Element|null} el */
    function describe(el) {
      if (!el) return '(該当なし / null)';
      const id = el.id ? `#${el.id}` : '';
      const classAttr =
        typeof el.className === 'string' && el.className.trim()
          ? `.${el.className.trim().split(/\s+/).join('.')}`
          : '';
      const text = (el.textContent || '').trim().replace(/\s+/g, ' ').slice(0, 40);
      return `<${el.tagName.toLowerCase()}${id}${classAttr}>${text ? ` "${text}"` : ''}`;
    }

    /** Builds a short CSS-ish path for debugging (not guaranteed unique). */
    function buildSelector(el) {
      if (!el) return null;
      const parts = [];
      let node = el;
      let depth = 0;
      while (node && node.nodeType === 1 && depth < 5) {
        if (node.id) {
          parts.unshift(`${node.tagName.toLowerCase()}#${node.id}`);
          break;
        }
        let part = node.tagName.toLowerCase();
        if (typeof node.className === 'string' && node.className.trim()) {
          part += `.${node.className.trim().split(/\s+/).join('.')}`;
        }
        parts.unshift(part);
        node = node.parentElement;
        depth += 1;
      }
      return parts.join(' > ');
    }

    /**
     * True rendered visibility, derived only from computed style + geometry.
     * Intentionally ignores the `hidden` attribute itself (see file header).
     */
    function isRenderedVisible(el) {
      const style = window.getComputedStyle(el);
      if (style.display === 'none') return false;
      if (style.visibility === 'hidden' || style.visibility === 'collapse') return false;
      if (Number(style.opacity) === 0) return false;

      const rect = el.getBoundingClientRect();
      if (rect.width <= 0 || rect.height <= 0) return false;

      return true;
    }

    /**
     * `aria-hidden="true"` (on the element itself or an ancestor) is an
     * explicit, unambiguous author declaration that the element is not
     * meant to be exposed for interaction -- e.g. csv-import-page.js's
     * native `<input type="file">` is deliberately `aria-hidden="true"`
     * and visually tiny, with the *visible* "ファイルを選択" button as the
     * real, intended tap target that triggers it programmatically. Testing
     * such elements as if a user were meant to tap them directly would be
     * testing the wrong thing.
     */
    function hasAriaHiddenSelfOrAncestor(el) {
      let node = el;
      while (node && node.nodeType === 1) {
        if (node.getAttribute && node.getAttribute('aria-hidden') === 'true') return true;
        node = node.parentElement;
      }
      return false;
    }

    // If a true modal dialog (`aria-modal="true"` or `role="dialog"`) is
    // currently open, only its own controls are meant to be reachable --
    // by definition, a modal dialog blocks interaction with the rest of
    // the page. Without this, opening e.g. the subscription modal would
    // make this sweep incorrectly flag the (intentionally inert) "+ 新規追加"
    // button and hamburger menu behind it as "blocked by an overlay", when
    // that's the whole point of a modal.
    const openModal = Array.from(
      document.querySelectorAll('[aria-modal="true"], [role="dialog"]')
    ).find(isRenderedVisible);
    const searchRoot = openModal || document;

    const candidates = Array.from(searchRoot.querySelectorAll(selector))
      .filter(isRenderedVisible)
      .filter((el) => !hasAriaHiddenSelfOrAncestor(el));

    const failures = [];

    for (const el of candidates) {
      // Scroll the element into view first so long pages are handled the
      // same way a real user would (scroll, then tap).
      el.scrollIntoView({ block: 'center', inline: 'center', behavior: 'instant' });

      const rect = el.getBoundingClientRect();
      if (rect.width <= 0 || rect.height <= 0) continue; // became hidden after scroll (rare)

      // Elements that are `position: fixed`/`sticky` and moved off-canvas
      // via a CSS transform (e.g. a closed mobile drawer with
      // `translateX(-100%)`) cannot be scrolled into view -- scrollIntoView
      // above is a no-op for them. Such elements are not currently
      // reachable by any real tap, so they must be skipped rather than
      // hit-tested at a clamped/fabricated coordinate (which would produce
      // false positives, since that coordinate isn't actually where the
      // element visually is).
      const intersectsViewport =
        rect.right > 0 &&
        rect.bottom > 0 &&
        rect.left < window.innerWidth &&
        rect.top < window.innerHeight;
      if (!intersectsViewport) continue;

      // Tap the center of the portion of the element that is actually
      // within the viewport (relevant when an element is only partially
      // scrolled/clipped into view).
      const visLeft = Math.max(rect.left, 0);
      const visTop = Math.max(rect.top, 0);
      const visRight = Math.min(rect.right, window.innerWidth);
      const visBottom = Math.min(rect.bottom, window.innerHeight);
      const cx = (visLeft + visRight) / 2;
      const cy = (visTop + visBottom) / 2;

      const hit = document.elementFromPoint(cx, cy);

      const isSelfOrDescendant = !!hit && el.contains(hit);
      const isAncestor = !!hit && hit.contains(el);
      const isAssociatedLabel =
        !!hit && hit.tagName === 'LABEL' && 'control' in hit && hit.control === el;

      if (!hit || (!isSelfOrDescendant && !isAncestor && !isAssociatedLabel)) {
        failures.push({
          target: describe(el),
          targetSelector: buildSelector(el),
          coveredBy: describe(hit),
          coveredBySelector: buildSelector(hit),
          x: Math.round(cx),
          y: Math.round(cy),
        });
      }
    }

    return failures;
  }, INTERACTIVE_SELECTOR);
}

/**
 * Formats failures into a single human-readable multi-line string suitable
 * for use as a Jest/Playwright assertion message, so a failing test
 * immediately tells you which element is blocked and by what.
 *
 * @param {string} screenName
 * @param {Array<object>} failures
 * @returns {string}
 */
function formatHitTestFailures(screenName, failures) {
  if (failures.length === 0) return '';

  const lines = failures.map((f, i) => {
    return (
      `  ${i + 1}. 対象要素 ${f.target}\n` +
      `       (${f.targetSelector})\n` +
      `     はタップ座標 (${f.x}, ${f.y}) で別要素に覆われています:\n` +
      `     覆っている要素: ${f.coveredBy}\n` +
      `       (${f.coveredBySelector})`
    );
  });

  return (
    `[${screenName}] ${failures.length}件のタップ不能要素が見つかりました。\n` +
    `無関係な要素（透明オーバーレイ等）がタップを吸収しています:\n\n` +
    lines.join('\n\n')
  );
}

module.exports = { findHitTestFailures, formatHitTestFailures, INTERACTIVE_SELECTOR };
