import { auth } from './auth.js';

const BASE_URL = '/api';

class ApiError extends Error {
  constructor(status, message) {
    super(message);
    this.status = status;
  }
}

// ---------------------------------------------------------------------------
// Loading state manager
// ---------------------------------------------------------------------------

/**
 * Manages a global loading overlay shown during API requests.
 *
 * - Shows a spinner overlay when one or more requests are in-flight.
 * - Lazily creates the overlay element on first use.
 * - Ref-counts concurrent requests so the overlay stays visible until all
 *   pending calls complete.
 */
const loadingManager = (() => {
  let activeRequests = 0;
  let overlayEl = null;

  function getOrCreateOverlay() {
    if (overlayEl) return overlayEl;

    overlayEl = document.createElement('div');
    overlayEl.className = 'loading-overlay';
    overlayEl.setAttribute('role', 'status');
    overlayEl.setAttribute('aria-label', '読み込み中');
    overlayEl.setAttribute('aria-live', 'polite');
    overlayEl.innerHTML = `
      <div class="loading-overlay__inner">
        <div class="spinner spinner--lg" aria-hidden="true"></div>
        <span>読み込み中...</span>
      </div>
    `;
    return overlayEl;
  }

  return {
    show() {
      activeRequests += 1;
      if (activeRequests === 1) {
        const el = getOrCreateOverlay();
        document.body.appendChild(el);
      }
    },

    hide() {
      activeRequests = Math.max(0, activeRequests - 1);
      if (activeRequests === 0 && overlayEl && overlayEl.parentNode) {
        overlayEl.parentNode.removeChild(overlayEl);
      }
    },
  };
})();

export { loadingManager };

// ---------------------------------------------------------------------------
// Network error banner
// ---------------------------------------------------------------------------

/**
 * Shows a brief "offline / network error" banner at the top of the page.
 * Auto-hides after 4 seconds.
 */
function showNetworkErrorBanner(message = 'ネットワークエラーが発生しました。接続を確認してください。') {
  let banner = document.querySelector('.network-error-banner');
  if (!banner) {
    banner = document.createElement('div');
    banner.className = 'network-error-banner';
    banner.setAttribute('role', 'alert');
    banner.setAttribute('aria-live', 'assertive');
    document.body.prepend(banner);
  }

  banner.textContent = message;
  banner.classList.add('network-error-banner--visible');

  clearTimeout(banner._hideTimer);
  banner._hideTimer = setTimeout(() => {
    banner.classList.remove('network-error-banner--visible');
  }, 4000);
}

// ---------------------------------------------------------------------------
// Core request function
// ---------------------------------------------------------------------------

async function request(method, path, body = null, { showLoader = true } = {}) {
  const headers = { 'Content-Type': 'application/json' };
  const token = auth.getToken();
  if (token) headers['Authorization'] = `Bearer ${token}`;

  const options = { method, headers };
  if (body !== null) options.body = JSON.stringify(body);

  if (showLoader) loadingManager.show();

  try {
    const res = await fetch(`${BASE_URL}${path}`, options);

    if (res.status === 401) {
      auth.logout();
      window.location.href = '/login';
      return;
    }

    if (!res.ok) {
      const err = await res.json().catch(() => ({ error: 'Unknown error' }));
      throw new ApiError(res.status, err.error || 'Request failed');
    }

    if (res.status === 204) return null;
    return res.json();
  } catch (err) {
    // Network-level failures (fetch throws TypeError when offline)
    if (err instanceof TypeError) {
      showNetworkErrorBanner();
      throw new ApiError(0, 'ネットワークエラーが発生しました。接続を確認してください。');
    }
    throw err;
  } finally {
    if (showLoader) loadingManager.hide();
  }
}

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

export const api = {
  get: (path, opts) => request('GET', path, null, opts),
  post: (path, body, opts) => request('POST', path, body, opts),
  put: (path, body, opts) => request('PUT', path, body, opts),
  delete: (path, opts) => request('DELETE', path, null, opts),

  async uploadFile(path, formData, { showLoader = true } = {}) {
    const headers = {};
    const token = auth.getToken();
    if (token) headers['Authorization'] = `Bearer ${token}`;

    if (showLoader) loadingManager.show();

    try {
      const res = await fetch(`${BASE_URL}${path}`, {
        method: 'POST',
        headers,
        body: formData
      });

      if (res.status === 401) {
        auth.logout();
        window.location.href = '/login';
        return;
      }

      if (!res.ok) {
        const err = await res.json().catch(() => ({ error: 'Upload failed' }));
        throw new ApiError(res.status, err.error || 'Upload failed');
      }
      return res.json();
    } catch (err) {
      if (err instanceof TypeError) {
        showNetworkErrorBanner();
        throw new ApiError(0, 'ネットワークエラーが発生しました。接続を確認してください。');
      }
      throw err;
    } finally {
      if (showLoader) loadingManager.hide();
    }
  }
};
