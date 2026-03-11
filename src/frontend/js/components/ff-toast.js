/**
 * ff-toast - Toast notification component
 *
 * Displays brief success or error messages in the top-right corner.
 * Auto-dismisses after a configurable duration.
 *
 * Usage (imperative, via singleton):
 *   import { toast } from './ff-toast.js';
 *   toast.show('保存しました', 'success');
 *   toast.show('エラーが発生しました', 'error');
 *
 * Usage (declarative):
 *   <ff-toast></ff-toast>
 */
class FfToast extends HTMLElement {
  constructor() {
    super();
    this.attachShadow({ mode: 'open' });
    this._queue = [];
    this._isShowing = false;
  }

  connectedCallback() {
    this._render();
    // Register as the global singleton
    FfToast._instance = this;
  }

  disconnectedCallback() {
    if (FfToast._instance === this) {
      FfToast._instance = null;
    }
  }

  /**
   * Show a toast notification
   * @param {string} message   - The message text to display
   * @param {'success'|'error'|'warning'|'info'} type - Visual style
   * @param {number} duration  - Auto-dismiss delay in ms (default 3000)
   */
  show(message, type = 'success', duration = 3000) {
    this._queue.push({ message, type, duration });
    if (!this._isShowing) {
      this._processQueue();
    }
  }

  _processQueue() {
    if (this._queue.length === 0) {
      this._isShowing = false;
      return;
    }

    this._isShowing = true;
    const { message, type, duration } = this._queue.shift();
    this._showItem(message, type, duration);
  }

  _showItem(message, type, duration) {
    const container = this.shadowRoot.querySelector('.toast-container');
    if (!container) return;

    const toast = document.createElement('div');
    toast.className = `toast toast--${type}`;
    toast.setAttribute('role', 'alert');
    toast.setAttribute('aria-live', 'polite');

    const icon = this._getIcon(type);
    toast.innerHTML = `
      <span class="toast__icon" aria-hidden="true">${icon}</span>
      <span class="toast__message">${this._escapeHtml(message)}</span>
      <button class="toast__close" aria-label="閉じる" type="button">&#x2715;</button>
    `;

    const closeBtn = toast.querySelector('.toast__close');
    closeBtn.addEventListener('click', () => this._dismiss(toast));

    container.appendChild(toast);

    // Trigger entrance animation
    requestAnimationFrame(() => {
      requestAnimationFrame(() => toast.classList.add('toast--visible'));
    });

    // Auto-dismiss
    const timer = setTimeout(() => this._dismiss(toast), duration);
    toast._dismissTimer = timer;
  }

  _dismiss(toast) {
    clearTimeout(toast._dismissTimer);
    toast.classList.remove('toast--visible');
    toast.classList.add('toast--hidden');
    toast.addEventListener('transitionend', () => {
      toast.remove();
      this._processQueue();
    }, { once: true });
  }

  _getIcon(type) {
    const icons = {
      success: '✓',
      error: '✕',
      warning: '⚠',
      info: 'ℹ',
    };
    return icons[type] || icons.info;
  }

  _render() {
    this.shadowRoot.innerHTML = `
      <style>
        :host {
          position: fixed;
          top: 80px;
          right: 24px;
          z-index: 600;
          display: flex;
          flex-direction: column;
          gap: 8px;
          pointer-events: none;
        }

        .toast-container {
          display: flex;
          flex-direction: column;
          gap: 8px;
        }

        .toast {
          display: flex;
          align-items: center;
          gap: 8px;
          padding: 12px 16px;
          border-radius: 8px;
          box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
          font-size: 0.875rem;
          font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
          max-width: 360px;
          min-width: 240px;
          pointer-events: all;
          opacity: 0;
          transform: translateX(100%);
          transition: opacity 250ms ease, transform 250ms ease;
        }

        .toast--visible {
          opacity: 1;
          transform: translateX(0);
        }

        .toast--hidden {
          opacity: 0;
          transform: translateX(100%);
        }

        .toast--success {
          background-color: #ECFDF5;
          border: 1px solid #BBF7D0;
          color: #059669;
        }

        .toast--error {
          background-color: #FEF2F2;
          border: 1px solid #FECACA;
          color: #DC2626;
        }

        .toast--warning {
          background-color: #FFFBEB;
          border: 1px solid #FDE68A;
          color: #D97706;
        }

        .toast--info {
          background-color: #ECFEFF;
          border: 1px solid #A5F3FC;
          color: #0891B2;
        }

        .toast__icon {
          font-size: 1rem;
          font-weight: 700;
          flex-shrink: 0;
        }

        .toast__message {
          flex: 1;
          line-height: 1.4;
        }

        .toast__close {
          background: none;
          border: none;
          cursor: pointer;
          color: inherit;
          font-size: 0.875rem;
          padding: 0;
          line-height: 1;
          opacity: 0.6;
          flex-shrink: 0;
          transition: opacity 150ms ease;
        }

        .toast__close:hover {
          opacity: 1;
        }
      </style>
      <div class="toast-container" aria-label="通知エリア"></div>
    `;
  }

  _escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
  }
}

customElements.define('ff-toast', FfToast);

/**
 * Convenience singleton accessor.
 * Falls back to creating a temporary element if the component is not mounted.
 */
export const toast = {
  show(message, type = 'success', duration = 3000) {
    if (FfToast._instance) {
      FfToast._instance.show(message, type, duration);
    } else {
      // Lazily mount the component if not yet in the DOM
      const el = document.createElement('ff-toast');
      document.body.appendChild(el);
      // Wait for connectedCallback to register the instance
      requestAnimationFrame(() => {
        FfToast._instance?.show(message, type, duration);
      });
    }
  },
};
