import { router } from '../router.js';

/**
 * ff-sidebar - App navigation sidebar component
 *
 * Renders navigation links for the main sections of the app.
 * Highlights the currently active route.
 *
 * Usage: <ff-sidebar active-path="/expenses"></ff-sidebar>
 *
 * Observed attributes:
 *   active-path  — the current route path, used to set the active link
 */
class FfSidebar extends HTMLElement {
  static get observedAttributes() {
    return ['active-path'];
  }

  constructor() {
    super();
    this.attachShadow({ mode: 'open' });
  }

  connectedCallback() {
    this._render();
    this._setupEventListeners();
  }

  attributeChangedCallback(name, oldValue, newValue) {
    if (name === 'active-path' && oldValue !== newValue) {
      this._updateActiveLink(newValue);
    }
  }

  /** Called by the router to update the active navigation item */
  setActivePath(path) {
    this.setAttribute('active-path', path);
  }

  _navItems() {
    return [
      { path: '/', label: 'ダッシュボード', icon: '📊' },
      { path: '/expenses', label: '支出一覧', icon: '💴' },
      { path: '/subscriptions', label: 'サブスク管理', icon: '🔄' },
      { path: '/categories', label: 'カテゴリ', icon: '🏷️' },
      { path: '/expenses/import', label: 'CSV取込', icon: '📥' },
    ];
  }

  _render() {
    const activePath = this.getAttribute('active-path') || window.location.pathname;

    const navLinksHtml = this._navItems()
      .map(item => {
        const isActive = this._isActive(item.path, activePath);
        const activeClass = isActive ? ' sidebar__link--active' : '';
        return `
          <li class="sidebar__nav-item">
            <a href="${item.path}"
               class="sidebar__link${activeClass}"
               data-navigo
               data-path="${item.path}">
              <span class="sidebar__link-icon" aria-hidden="true">${item.icon}</span>
              <span>${this._escapeHtml(item.label)}</span>
            </a>
          </li>
        `;
      })
      .join('');

    this.shadowRoot.innerHTML = `
      <style>
        :host {
          display: block;
        }

        .sidebar {
          position: fixed;
          top: 60px;
          left: 0;
          width: 240px;
          height: calc(100vh - 60px);
          background-color: #ffffff;
          border-right: 1px solid #E5E7EB;
          display: flex;
          flex-direction: column;
          z-index: 100;
          overflow-y: auto;
          transition: transform 300ms ease;
        }

        .sidebar--hidden {
          transform: translateX(-100%);
        }

        .sidebar__nav {
          list-style: none;
          padding: 16px 0;
          flex: 1;
          margin: 0;
        }

        .sidebar__nav-item {
          margin: 2px 8px;
        }

        .sidebar__link {
          display: flex;
          align-items: center;
          gap: 8px;
          padding: 8px 16px;
          color: #111827;
          text-decoration: none;
          border-radius: 8px;
          font-size: 0.875rem;
          font-weight: 500;
          font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
          transition: all 200ms ease;
        }

        .sidebar__link:hover {
          background-color: #EFF6FF;
          color: #3B82F6;
        }

        .sidebar__link--active {
          background-color: #EFF6FF;
          color: #3B82F6;
          font-weight: 600;
        }

        .sidebar__link-icon {
          font-size: 1rem;
          width: 1.25rem;
          text-align: center;
        }
      </style>

      <nav class="sidebar" role="navigation" aria-label="メインナビゲーション">
        <ul class="sidebar__nav">
          ${navLinksHtml}
        </ul>
      </nav>
    `;
  }

  _setupEventListeners() {
    this.shadowRoot.addEventListener('click', (e) => {
      const link = e.target.closest('[data-navigo]');
      if (!link) return;
      e.preventDefault();
      const path = link.getAttribute('data-path');
      router.navigate(path);
    });
  }

  _updateActiveLink(activePath) {
    const links = this.shadowRoot.querySelectorAll('.sidebar__link');
    links.forEach(link => {
      const linkPath = link.getAttribute('data-path');
      const isActive = this._isActive(linkPath, activePath);
      link.classList.toggle('sidebar__link--active', isActive);
    });
  }

  _isActive(linkPath, currentPath) {
    if (linkPath === '/') {
      return currentPath === '/';
    }
    return currentPath === linkPath || currentPath.startsWith(linkPath + '/');
  }

  _escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
  }
}

customElements.define('ff-sidebar', FfSidebar);
