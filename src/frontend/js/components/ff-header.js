import { auth } from '../utils/auth.js';
import { router } from '../router.js';

/**
 * ff-header - App header component
 *
 * Displays the FinFlow logo, current username, and logout button.
 * Fires a custom 'ff-logout' event on the document when logout is clicked.
 *
 * Usage: <ff-header></ff-header>
 */
class FfHeader extends HTMLElement {
  constructor() {
    super();
    this.attachShadow({ mode: 'open' });
    this._handleLogout = this._handleLogout.bind(this);
    this._handleBrandClick = () => router.navigate('/');
  }

  connectedCallback() {
    this._render();
    this._setupEventListeners();
  }

  disconnectedCallback() {
    const logoutBtn = this.shadowRoot.querySelector('.header__logout-btn');
    logoutBtn?.removeEventListener('click', this._handleLogout);

    const brandBtn = this.shadowRoot.querySelector('.header__brand');
    brandBtn?.removeEventListener('click', this._handleBrandClick);
  }

  /** Update displayed username without re-rendering the entire shadow DOM */
  refresh() {
    this._updateUsername();
  }

  _render() {
    this.shadowRoot.innerHTML = `
      <style>
        :host {
          display: block;
        }

        .header {
          position: fixed;
          top: 0;
          left: 0;
          right: 0;
          height: 60px;
          background-color: #ffffff;
          border-bottom: 1px solid #E5E7EB;
          display: flex;
          align-items: center;
          justify-content: space-between;
          padding: 0 24px;
          z-index: 200;
          box-shadow: 0 1px 2px rgba(0, 0, 0, 0.05);
        }

        .header__brand {
          display: flex;
          align-items: center;
          gap: 8px;
          font-size: 1.25rem;
          font-weight: 700;
          color: #3B82F6;
          text-decoration: none;
          cursor: pointer;
          border: none;
          background: none;
          font-family: inherit;
        }

        .header__brand-icon {
          font-size: 1.5rem;
        }

        .header__right {
          display: flex;
          align-items: center;
          gap: 16px;
        }

        .header__username {
          font-size: 0.875rem;
          color: #374151;
        }

        .header__logout-btn {
          padding: 4px 16px;
          font-size: 0.875rem;
          background: none;
          border: 1px solid #E5E7EB;
          border-radius: 8px;
          color: #6B7280;
          cursor: pointer;
          font-family: inherit;
          transition: all 200ms ease;
        }

        .header__logout-btn:hover {
          background-color: #EF4444;
          color: #ffffff;
          border-color: #EF4444;
        }
      </style>

      <header class="header" role="banner">
        <button class="header__brand" aria-label="FinFlow ホームへ">
          <span class="header__brand-icon" aria-hidden="true">💰</span>
          <span>FinFlow</span>
        </button>
        <div class="header__right">
          <span class="header__username" hidden></span>
          <button class="header__logout-btn" type="button">ログアウト</button>
        </div>
      </header>
    `;

    this._updateUsername();
  }

  _updateUsername() {
    const user = auth.getUser();
    const displayName = user ? (user.name || user.email || 'ユーザー') : '';
    const usernameEl = this.shadowRoot.querySelector('.header__username');
    if (!usernameEl) return;

    if (displayName) {
      // textContent assignment is XSS-safe; no escaping needed
      usernameEl.textContent = `${displayName} さん`;
      usernameEl.removeAttribute('hidden');
    } else {
      usernameEl.textContent = '';
      usernameEl.setAttribute('hidden', '');
    }
  }

  _setupEventListeners() {
    const logoutBtn = this.shadowRoot.querySelector('.header__logout-btn');
    logoutBtn?.addEventListener('click', this._handleLogout);

    const brandBtn = this.shadowRoot.querySelector('.header__brand');
    brandBtn?.addEventListener('click', this._handleBrandClick);
  }

  _handleLogout() {
    auth.logout();
    document.dispatchEvent(new CustomEvent('ff-logout'));
    router.navigate('/login');
  }
}

customElements.define('ff-header', FfHeader);
