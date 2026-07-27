/**
 * ff-confirm-dialog - Confirmation modal dialog component
 *
 * Replaces browser's native confirm() with a custom modal.
 * Returns a Promise that resolves to true (confirmed) or false (cancelled).
 *
 * Usage (imperative, via singleton):
 *   import { confirmDialog } from './ff-confirm-dialog.js';
 *   const ok = await confirmDialog.show({
 *     title: '削除の確認',
 *     message: 'この支出を削除しますか？この操作は取り消せません。',
 *     confirmLabel: '削除する',    // default: '確認'
 *     cancelLabel: 'キャンセル',   // default: 'キャンセル'
 *     danger: true,               // default: false — makes confirm button red
 *   });
 *   if (ok) { ... }
 *
 * Usage (declarative):
 *   <ff-confirm-dialog></ff-confirm-dialog>
 */
class FfConfirmDialog extends HTMLElement {
  constructor() {
    super();
    this.attachShadow({ mode: 'open' });
    this._resolve = null;
    this._handleKeydown = this._handleKeydown.bind(this);
  }

  connectedCallback() {
    this._render();
    FfConfirmDialog._instance = this;
  }

  disconnectedCallback() {
    if (FfConfirmDialog._instance === this) {
      FfConfirmDialog._instance = null;
    }
    document.removeEventListener('keydown', this._handleKeydown);
  }

  /**
   * Show the confirmation dialog.
   * @param {object} options
   * @param {string} options.title
   * @param {string} options.message
   * @param {string} [options.confirmLabel]
   * @param {string} [options.cancelLabel]
   * @param {boolean} [options.danger]
   * @returns {Promise<boolean>}
   */
  show({ title, message, confirmLabel = '確認', cancelLabel = 'キャンセル', danger = false } = {}) {
    // このコンポーネントはシングルトンなので、前の show() が未解決のまま
    // 次の show() が来ると _resolve が上書きされ、前の Promise が
    // **永久に解決しない**（await している呼び出し元がそこで固まる）。
    // 呼び出し側にもガードを置いているが、ここでも必ず決着させる。
    if (this._resolve) {
      const previous = this._resolve;
      this._resolve = null;
      previous(false);
    }

    return new Promise((resolve) => {
      this._resolve = resolve;
      this._renderDialog({ title, message, confirmLabel, cancelLabel, danger });
      this._openDialog();
    });
  }

  _openDialog() {
    const overlay = this.shadowRoot.querySelector('.dialog-overlay');
    overlay?.classList.add('dialog-overlay--visible');
    document.addEventListener('keydown', this._handleKeydown);

    // Focus the cancel button by default (safer default for destructive actions)
    requestAnimationFrame(() => {
      this.shadowRoot.querySelector('.dialog__cancel-btn')?.focus();
    });
  }

  _closeDialog(result) {
    const overlay = this.shadowRoot.querySelector('.dialog-overlay');
    overlay?.classList.remove('dialog-overlay--visible');
    document.removeEventListener('keydown', this._handleKeydown);

    if (this._resolve) {
      this._resolve(result);
      this._resolve = null;
    }
  }

  _handleKeydown(e) {
    if (e.key === 'Escape') {
      this._closeDialog(false);
    }
    if (e.key === 'Enter') {
      const confirmBtn = this.shadowRoot.querySelector('.dialog__confirm-btn');
      const focusedEl = this.shadowRoot.activeElement;
      if (focusedEl !== confirmBtn) return;
      this._closeDialog(true);
    }
  }

  _renderDialog({ title, message, confirmLabel, cancelLabel, danger }) {
    const confirmBtnClass = `dialog__confirm-btn${danger ? ' dialog__confirm-btn--danger' : ''}`;

    // Update title and message via text content (XSS-safe)
    const titleEl = this.shadowRoot.querySelector('.dialog__title');
    const messageEl = this.shadowRoot.querySelector('.dialog__message');
    const confirmBtn = this.shadowRoot.querySelector('.dialog__confirm-btn');
    const cancelBtn = this.shadowRoot.querySelector('.dialog__cancel-btn');

    if (titleEl && messageEl && confirmBtn && cancelBtn) {
      titleEl.textContent = title;
      messageEl.textContent = message;
      confirmBtn.textContent = confirmLabel;
      confirmBtn.className = confirmBtnClass;
      cancelBtn.textContent = cancelLabel;
    } else {
      // First render
      this._renderInitial({ title, message, confirmLabel, cancelLabel, danger });
    }
  }

  _renderInitial({ title, message, confirmLabel, cancelLabel, danger }) {
    const confirmBtnClass = `dialog__confirm-btn${danger ? ' dialog__confirm-btn--danger' : ''}`;

    this.shadowRoot.innerHTML = `
      <style>
        /* ---------------------------------------------------------------
           Shadow DOM のため、main.css のモバイル規約（@media (max-width:768px)
           の .btn { min-height:44px } 等）は**ここには届かない**。
           そのためタップ領域・ボトムシート化はこのファイル内に自前で書く。
           CSS カスタムプロパティは Shadow 境界を越えて継承されるので、
           :root のトークンはそのまま参照できる。
           --------------------------------------------------------------- */
        :host {
          display: block;
        }

        /* 閉じている間は opacity だけでなく visibility も落とす。
           opacity:0 だけだと要素はレイアウト上に残り続けるため、
           - Tab キーで「見えないボタン」にフォーカスが入る
           - 支援技術からは読み上げ可能なまま
           という状態になる（.sidebar-overlay が全タップを吸っていた
           不具合と同じ「見えないのに生きている」系の問題）。
           visibility の transition は遅延 0s で切り替え、
           フェードアウトが終わってから消えるようにする。 */
        .dialog-overlay {
          position: fixed;
          inset: 0;
          background-color: rgba(17, 24, 39, 0.55);
          display: flex;
          align-items: center;
          justify-content: center;
          z-index: 500;
          opacity: 0;
          visibility: hidden;
          pointer-events: none;
          transition: opacity 180ms ease, visibility 0s linear 180ms;
          padding: 16px;
        }

        .dialog-overlay--visible {
          opacity: 1;
          visibility: visible;
          pointer-events: all;
          transition: opacity 180ms ease, visibility 0s linear 0s;
        }

        .dialog {
          /* --color-bg は薄グレー(#F9FAFB)の「ページ背景」トークン。
             カードやダイアログの面は --color-surface(白)が正しい。
             継承で --color-bg が効いてしまい、ダイアログだけ灰色になっていた。 */
          background: var(--color-surface, #ffffff);
          border-radius: var(--border-radius-lg, 16px);
          box-shadow: var(--shadow-lg, 0 20px 25px rgba(0, 0, 0, 0.15));
          padding: 24px;
          width: 100%;
          max-width: 400px;
          transform: scale(0.96);
          transition: transform 180ms ease;
        }

        .dialog-overlay--visible .dialog {
          transform: scale(1);
        }

        .dialog__title {
          font-size: 1.125rem;
          font-weight: 700;
          color: var(--color-text, #111827);
          margin-bottom: 12px;
          font-family: var(--font-family, -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif);
        }

        .dialog__message {
          font-size: 0.9375rem;
          color: var(--color-text-secondary, #4B5563);
          line-height: 1.6;
          margin-bottom: 24px;
          overflow-wrap: anywhere;
          font-family: var(--font-family, -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif);
        }

        .dialog__actions {
          display: flex;
          gap: 12px;
          justify-content: flex-end;
        }

        .dialog__cancel-btn,
        .dialog__confirm-btn {
          display: inline-flex;
          align-items: center;
          justify-content: center;
          min-height: var(--tap-target, 48px);
          padding: 10px 20px;
          border-radius: var(--border-radius, 10px);
          font-size: 0.9375rem;
          font-weight: 600;
          font-family: var(--font-family, -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif);
          cursor: pointer;
          /* タップの即応性: ダブルタップズーム待ちを無くし、
             iOS の灰色ハイライトの代わりに :active で自前のフィードバックを出す。 */
          touch-action: manipulation;
          -webkit-tap-highlight-color: transparent;
          user-select: none;
          -webkit-user-select: none;
          transition: background-color 150ms ease, transform 80ms ease;
        }

        .dialog__cancel-btn:active,
        .dialog__confirm-btn:active {
          transform: scale(0.97);
        }

        .dialog__cancel-btn {
          background: var(--color-surface, #ffffff);
          border: 1px solid var(--color-border, #E5E7EB);
          color: var(--color-text, #374151);
        }

        .dialog__cancel-btn:hover {
          background-color: var(--color-muted-bg, #F3F4F6);
        }

        .dialog__confirm-btn {
          background-color: var(--color-primary, #3B82F6);
          border: 1px solid var(--color-primary, #3B82F6);
          color: #ffffff;
        }

        .dialog__confirm-btn:hover {
          background-color: var(--color-primary-dark, #2563EB);
          border-color: var(--color-primary-dark, #2563EB);
        }

        .dialog__confirm-btn--danger {
          background-color: var(--color-danger, #EF4444);
          border-color: var(--color-danger, #EF4444);
        }

        .dialog__confirm-btn--danger:hover {
          background-color: #DC2626;
          border-color: #DC2626;
        }

        .dialog__cancel-btn:focus-visible,
        .dialog__confirm-btn:focus-visible {
          outline: 2px solid var(--color-primary, #3B82F6);
          outline-offset: 2px;
        }

        /* モバイル: 下から出るシート。ボタンは全幅・縦積みにして、
           親指の届く下側に主要な操作を置く。DOM 順（キャンセル → 確認）が
           そのまま上下になるので、破壊的な「削除する」が下に来る。 */
        @media (max-width: 768px) {
          .dialog-overlay {
            align-items: flex-end;
            padding: 0;
          }

          .dialog {
            max-width: none;
            border-radius: var(--border-radius-lg, 16px) var(--border-radius-lg, 16px) 0 0;
            padding: 20px 20px calc(20px + var(--safe-bottom, 0px));
            transform: translateY(16px);
          }

          .dialog-overlay--visible .dialog {
            transform: translateY(0);
          }

          .dialog__actions {
            flex-direction: column;
            gap: 10px;
          }

          .dialog__cancel-btn,
          .dialog__confirm-btn {
            width: 100%;
            font-size: 1rem;
          }
        }

        @media (prefers-reduced-motion: reduce) {
          .dialog-overlay,
          .dialog,
          .dialog__cancel-btn,
          .dialog__confirm-btn {
            transition: none;
          }
          .dialog__cancel-btn:active,
          .dialog__confirm-btn:active {
            transform: none;
          }
        }
      </style>

      <div class="dialog-overlay" role="dialog" aria-modal="true" aria-labelledby="dialog-title">
        <div class="dialog">
          <h2 class="dialog__title" id="dialog-title"></h2>
          <p class="dialog__message"></p>
          <div class="dialog__actions">
            <button class="dialog__cancel-btn" type="button"></button>
            <button class="${confirmBtnClass}" type="button"></button>
          </div>
        </div>
      </div>
    `;

    // Set text content safely (no XSS)
    this.shadowRoot.querySelector('.dialog__title').textContent = title;
    this.shadowRoot.querySelector('.dialog__message').textContent = message;
    this.shadowRoot.querySelector('.dialog__confirm-btn').textContent = confirmLabel;
    this.shadowRoot.querySelector('.dialog__cancel-btn').textContent = cancelLabel;

    this._attachDialogListeners();
  }

  _attachDialogListeners() {
    const confirmBtn = this.shadowRoot.querySelector('.dialog__confirm-btn');
    const cancelBtn = this.shadowRoot.querySelector('.dialog__cancel-btn');
    const overlay = this.shadowRoot.querySelector('.dialog-overlay');

    confirmBtn?.addEventListener('click', () => this._closeDialog(true));
    cancelBtn?.addEventListener('click', () => this._closeDialog(false));

    // Click outside dialog closes it (cancel)
    overlay?.addEventListener('click', (e) => {
      if (e.target === overlay) this._closeDialog(false);
    });
  }

  _render() {
    this._renderInitial({
      title: '',
      message: '',
      confirmLabel: '確認',
      cancelLabel: 'キャンセル',
      danger: false,
    });
  }
}

customElements.define('ff-confirm-dialog', FfConfirmDialog);

/** Singleton accessor */
export const confirmDialog = {
  show(options) {
    if (FfConfirmDialog._instance) {
      return FfConfirmDialog._instance.show(options);
    }
    // Lazily mount
    const el = document.createElement('ff-confirm-dialog');
    document.body.appendChild(el);
    return new Promise((resolve) => {
      requestAnimationFrame(() => {
        FfConfirmDialog._instance?.show(options).then(resolve);
      });
    });
  },
};
