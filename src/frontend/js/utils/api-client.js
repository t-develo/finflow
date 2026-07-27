import { auth } from './auth.js';

const BASE_URL = '/api';

/**
 * API リクエストのタイムアウト（ミリ秒）。
 * ラズパイ実機の起動直後（EF Core のマイグレーション適用中）でも通る程度に
 * 余裕を持たせつつ、ユーザーが「固まった」と感じる前に打ち切れる値。
 */
const REQUEST_TIMEOUT_MS = 15_000;

/**
 * ファイルアップロード（CSV取込）のタイムアウト（ミリ秒）。
 * 最大 10,000 行の取込をラズパイ実機で処理する時間を見込んで長めに取る。
 * 「無制限」にはしない — 応答が返らないと .loading-overlay が残り、
 * 画面全体が操作不能になるため。
 */
const UPLOAD_TIMEOUT_MS = 120_000;

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

    /**
     * 進行中カウントを問答無用で 0 に戻し、オーバーレイを DOM から外す。
     *
     * .loading-overlay は position:fixed; inset:0; z-index:500 の全画面要素で、
     * #page-container の外（document.body 直下）にあるためルート遷移でも消えない。
     * 何らかの理由で hide() が呼ばれ損ねると、画面全体が操作不能になり
     * 「見た目はほぼ普通なのにタップが効かない」状態になる。
     * ルート遷移時と 401 リダイレクト時の保険として使う。
     */
    reset() {
      activeRequests = 0;
      if (overlayEl && overlayEl.parentNode) {
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

  // タイムアウトを付ける理由:
  // fetch は既定でタイムアウトしない。家庭内 LAN + ラズパイという構成では
  // Wi-Fi の瞬断やサーバー側のハングで Promise が永久に解決しないことがあり、
  // その場合 finally が走らないため .loading-overlay が画面に残り続け、
  // 全画面が操作不能になる（= 今回の「前面のなにか」の再現経路のひとつ）。
  const controller = new AbortController();
  const timeoutId = setTimeout(() => controller.abort(), REQUEST_TIMEOUT_MS);

  const options = { method, headers, signal: controller.signal };
  if (body !== null) options.body = JSON.stringify(body);

  if (showLoader) loadingManager.show();

  try {
    const res = await fetch(`${BASE_URL}${path}`, options);

    if (res.status === 401) {
      auth.logout();
      // location.href の遷移が完了するまでの間、ローディング膜が残って
      // 操作不能に見えるのを防ぐ（finally より先に確実に外す）。
      loadingManager.reset();
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
    // タイムアウト（AbortController.abort()）は AbortError として飛んでくる
    if (err instanceof DOMException && err.name === 'AbortError') {
      showNetworkErrorBanner(
        'サーバーからの応答がありません。接続を確認して、もう一度お試しください。'
      );
      throw new ApiError(0, 'リクエストがタイムアウトしました。');
    }
    // Network-level failures (fetch throws TypeError when offline)
    if (err instanceof TypeError) {
      showNetworkErrorBanner();
      throw new ApiError(0, 'ネットワークエラーが発生しました。接続を確認してください。');
    }
    throw err;
  } finally {
    clearTimeout(timeoutId);
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

    // request() と同じ理由でタイムアウトを設ける（応答が返らないと
    // .loading-overlay が残り、画面全体が操作不能になる）。
    // ただし CSV 取込は最大 10,000 行を処理しうるので閾値は別にする。
    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), UPLOAD_TIMEOUT_MS);

    if (showLoader) loadingManager.show();

    try {
      const res = await fetch(`${BASE_URL}${path}`, {
        method: 'POST',
        headers,
        body: formData,
        signal: controller.signal
      });

      if (res.status === 401) {
        auth.logout();
        loadingManager.reset();
        window.location.href = '/login';
        return;
      }

      if (!res.ok) {
        const err = await res.json().catch(() => ({ error: 'Upload failed' }));
        throw new ApiError(res.status, err.error || 'Upload failed');
      }
      return res.json();
    } catch (err) {
      if (err instanceof DOMException && err.name === 'AbortError') {
        showNetworkErrorBanner(
          'アップロードがタイムアウトしました。ファイルのサイズと接続を確認してください。'
        );
        throw new ApiError(0, 'アップロードがタイムアウトしました。');
      }
      if (err instanceof TypeError) {
        showNetworkErrorBanner();
        throw new ApiError(0, 'ネットワークエラーが発生しました。接続を確認してください。');
      }
      throw err;
    } finally {
      clearTimeout(timeoutId);
      if (showLoader) loadingManager.hide();
    }
  }
};
