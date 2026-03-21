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
        :host {
          display: block;
        }

        .dialog-overlay {
          position: fixed;
          inset: 0;
          background-color: rgba(0, 0, 0, 0.5);
          display: flex;
          align-items: center;
          justify-content: center;
          z-index: 500;
          opacity: 0;
          pointer-events: none;
          transition: opacity 200ms ease;
          padding: 16px;
        }

        .dialog-overlay--visible {
          opacity: 1;
          pointer-events: all;
        }

        .dialog {
          background: var(--color-bg, #ffffff);
          border-radius: var(--border-radius-lg, 12px);
          box-shadow: 0 20px 25px rgba(0, 0, 0, 0.1);
          padding: 24px;
          width: 100%;
          max-width: 400px;
          transform: scale(0.95);
          transition: transform 200ms ease;
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
          font-size: 0.875rem;
          color: var(--color-text-secondary, #4B5563);
          line-height: 1.6;
          margin-bottom: 24px;
          font-family: var(--font-family, -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif);
        }

        .dialog__actions {
          display: flex;
          gap: 12px;
          justify-content: flex-end;
        }

        .dialog__cancel-btn,
        .dialog__confirm-btn {
          padding: 8px 20px;
          border-radius: var(--border-radius, 8px);
          font-size: 0.875rem;
          font-weight: 500;
          font-family: var(--font-family, -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif);
          cursor: pointer;
          transition: all 200ms ease;
        }

        .dialog__cancel-btn {
          background: #ffffff;
          border: 1px solid var(--color-border, #E5E7EB);
          color: var(--color-text, #374151);
        }

        .dialog__cancel-btn:hover {
          background-color: #F3F4F6;
        }

        .dialog__confirm-btn {
          background-color: var(--color-primary, #3B82F6);
          border: 1px solid var(--color-primary, #3B82F6);
          color: #ffffff;
        }

        .dialog__confirm-btn:hover {
          background-color: #2563EB;
          border-color: #2563EB;
        }

        .dialog__confirm-btn--danger {
          background-color: #EF4444;
          border-color: #EF4444;
        }

        .dialog__confirm-btn--danger:hover {
          background-color: #DC2626;
          border-color: #DC2626;
        }

        .dialog__cancel-btn:focus,
        .dialog__confirm-btn:focus {
          outline: 2px solid var(--color-primary, #3B82F6);
          outline-offset: 2px;
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
