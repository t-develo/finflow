/**
 * shadow-dom.js — Shadow DOM を貫通してタップ領域を検査するヘルパー。
 *
 * ------------------------------------------------------------------
 * なぜ必要か
 * ------------------------------------------------------------------
 * 既存の tap-target-size.spec.js / helpers/hit-test.js は
 * `document.querySelectorAll` で対象を集めている。これは **Shadow DOM を
 * 貫通しない**ため、`ff-confirm-dialog` と `ff-toast` の中のボタンは
 * 一度も検査されていなかった。
 *
 * 実際そこに穴があった:
 *  - ff-confirm-dialog の「削除する / キャンセル」は実測 34px
 *    （main.css の `@media (max-width:768px){ .btn{min-height:44px} }` は
 *      Shadow 境界を越えないので届かない）
 *  - ff-toast の閉じるボタンは実測 14px 四方
 *
 * どちらも「削除」の導線上にあり、「削除だけ反応が鈍い」体感に直結していた。
 */

'use strict';

/** WCAG 2.5.5 / iOS HIG の最小タップ領域。 */
const MIN_TAP_TARGET_PX = 44;

/**
 * 指定した範囲（Shadow DOM を含む）の操作可能要素のうち、
 * 表示されていてタップ領域が MIN_TAP_TARGET_PX 未満のものを返す。
 *
 * @param {import('@playwright/test').Page} page
 * @param {string} [rootSelector] 検査の起点。既定はページ全体。
 *   Shadow DOM を持つカスタム要素（'ff-confirm-dialog' など）を
 *   指定すると、その中だけを検査できる。
 * @returns {Promise<Array<{label: string, width: number, height: number}>>}
 */
async function tapTargetIssuesDeep(page, rootSelector = null) {
  return page.evaluate(({ minSize, rootSelector }) => {
    const INTERACTIVE = 'button, a[href], input, select, textarea, [role="button"]';
    const found = [];

    /**
     * root（Document か ShadowRoot）配下を走査し、見つけた要素の
     * shadowRoot があればそこも再帰的に辿る。
     */
    function walk(root, path) {
      for (const el of root.querySelectorAll('*')) {
        if (el.shadowRoot) {
          walk(el.shadowRoot, `${path}${el.tagName.toLowerCase()} >> `);
        }
        if (!el.matches(INTERACTIVE)) continue;

        const rect = el.getBoundingClientRect();
        // 非表示のものは対象外（当たり判定が無いので問題にならない）
        if (rect.width === 0 || rect.height === 0) continue;
        const style = getComputedStyle(el);
        if (style.visibility === 'hidden' || style.display === 'none') continue;
        if (style.pointerEvents === 'none') continue;

        if (rect.width < minSize || rect.height < minSize) {
          const text = (el.textContent || '').trim().slice(0, 24);
          const id = el.id ? `#${el.id}` : '';
          const cls = el.className && typeof el.className === 'string'
            ? `.${el.className.trim().split(/\s+/).join('.')}`
            : '';
          found.push({
            label: `${path}${el.tagName.toLowerCase()}${id}${cls}${text ? ` "${text}"` : ''}`,
            width: Math.round(rect.width),
            height: Math.round(rect.height),
          });
        }
      }
    }

    if (rootSelector) {
      for (const host of document.querySelectorAll(rootSelector)) {
        const prefix = `${host.tagName.toLowerCase()} >> `;
        if (host.shadowRoot) walk(host.shadowRoot, prefix);
        walk(host, prefix);
      }
    } else {
      walk(document, '');
    }
    return found;
  }, { minSize: MIN_TAP_TARGET_PX, rootSelector });
}

module.exports = { MIN_TAP_TARGET_PX, tapTargetIssuesDeep };
