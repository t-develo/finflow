import { auth } from './utils/auth.js';
import { loadingManager } from './utils/api-client.js';

const routes = new Map();
// Routes containing a ":param" segment (e.g. "/expenses/:id/edit") can't be
// looked up by exact string match, so they're kept separately and matched
// via regex, in registration order. Plain routes always take precedence
// (checked first via the `routes` Map), so a literal route like
// "/expenses/new" never falls through to a pattern like "/expenses/:id/edit".
const patternRoutes = [];

// Array (not a single variable) so multiple independent subscribers can
// each register their own onRouteChange() callback without one silently
// overwriting another's registration.
const routeChangeListeners = [];

// 現在表示中のページが返した後片付け関数（無ければ null）。
// ページハンドラが関数を返した場合だけ設定され、次の遷移の**直前**に呼ばれる。
// container.innerHTML = '' は #page-container の中しか消せないので、
// document / window に貼ったリスナーやタイマーはページ自身にしか片付けられない。
let activeCleanup = null;

/**
 * 前のページの後片付けを実行する。
 *
 * ページ側の例外で遷移そのものが止まらないよう握り潰す（後片付けの失敗より、
 * 画面が出ないことのほうが遥かに重い）。ここで throw を通すと、handleRoute の
 * 描画前で止まって**全ルートが真っ白**という以前の事故を再演することになる。
 */
function runActiveCleanup() {
  const cleanup = activeCleanup;
  activeCleanup = null;
  if (typeof cleanup !== 'function') return;
  try {
    cleanup();
  } catch (err) {
    console.error('[router] page cleanup failed', err);
  }
}

/**
 * Convert a route template ("/expenses/:id/edit") into a RegExp plus the
 * ordered list of param names it captures. Each ":param" segment matches
 * exactly one path segment ([^/]+) so it can never span a "/".
 */
function compilePattern(path) {
  const keys = [];
  const regexSource = path
    .split('/')
    .map((segment) => {
      if (segment.startsWith(':')) {
        keys.push(segment.slice(1));
        return '([^/]+)';
      }
      return segment.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    })
    .join('/');
  return { regex: new RegExp(`^${regexSource}$`), keys };
}

function matchRoute(path) {
  if (routes.has(path)) {
    return { handler: routes.get(path), params: {} };
  }
  for (const { regex, keys, handler } of patternRoutes) {
    const match = path.match(regex);
    if (match) {
      const params = {};
      keys.forEach((key, i) => { params[key] = match[i + 1]; });
      return { handler, params };
    }
  }
  return null;
}

function navigate(path) {
  window.history.pushState({}, '', path);
  handleRoute(path);
}

function handleRoute(path) {
  // 画面遷移のたびに、前の画面で出しっぱなしになったローディング膜を片付ける。
  // .loading-overlay は document.body 直下に付くため #page-container の
  // クリアでは消えず、残ると次の画面が丸ごと操作不能になる。
  //
  // オプショナル呼び出しにしているのは意図的。ビルドステップが無く、各モジュールが
  // それぞれ独立した URL / キャッシュエントリとして取得されるため、端末上で
  // 「新しい router.js + 古い api-client.js」という組み合わせが成立しうる。
  // reset() は後から追加したメソッドなので、素で呼ぶと古い api-client.js との
  // 組み合わせで TypeError になり、これは handleRoute() の最初の実行文
  // （container.innerHTML に触れる前）なので**全ルートが真っ白**になる。実際に発生した。
  // 詳細と規約は .claude/rules/javascript/hooks.md を参照。
  loadingManager?.reset?.();

  const sidebar = document.getElementById('sidebar');
  const mainContent = document.getElementById('main-content');
  const container = document.getElementById('page-container');

  if (!auth.isAuthenticated() && path !== '/login' && path !== '/register') {
    navigate('/login');
    return;
  }

  const isAuthPage = path === '/login' || path === '/register';
  if (isAuthPage) {
    sidebar.classList.add('sidebar--hidden');
    mainContent.classList.add('main-content--full');
  } else {
    sidebar.classList.remove('sidebar--hidden');
    mainContent.classList.remove('main-content--full');
    updateActiveLink(path);
  }

  routeChangeListeners.forEach((listener) => listener(isAuthPage));

  const matched = matchRoute(path);
  const handler = matched ? matched.handler : routes.get('*');
  if (handler) {
    // DOM を消す前に前ページの後片付けを走らせる。順序が逆だと、片付け側が
    // 自分の DOM を参照できず（既に消えている）null 参照になる。
    runActiveCleanup();
    container.innerHTML = '';

    // ページハンドラが関数を返したら、それを次の遷移時の後片付けとして預かる。
    // 返さないページは従来どおり何も起きない（全ページを一斉に書き換える必要はない）。
    const result = handler(container, matched ? matched.params : {});
    activeCleanup = typeof result === 'function' ? result : null;
  }
}

function updateActiveLink(path) {
  document.querySelectorAll('.sidebar__link').forEach(link => {
    link.classList.toggle('sidebar__link--active',
      link.getAttribute('href') === path);
  });
}

export const router = {
  on(path, handler) {
    if (path.includes(':')) {
      const { regex, keys } = compilePattern(path);
      patternRoutes.push({ regex, keys, handler });
    } else {
      routes.set(path, handler);
    }
    return this;
  },

  /**
   * Register a callback invoked on every route change with
   * `isAuthPage` (true for /login, /register). Used by app.js to keep
   * layout chrome (e.g. the hamburger button) in sync with routing
   * without router.js needing to know about layout elements.
   *
   * Multiple calls each add an independent listener (all are invoked, in
   * registration order) rather than the later call replacing the earlier
   * one.
   */
  onRouteChange(callback) {
    routeChangeListeners.push(callback);
  },

  start() {
    window.addEventListener('popstate', () => handleRoute(window.location.pathname));

    document.addEventListener('click', (e) => {
      const link = e.target.closest('[data-navigo]');
      if (link) {
        e.preventDefault();
        navigate(link.getAttribute('href'));
      }
    });

    handleRoute(window.location.pathname || '/dashboard');
  },

  navigate
};
