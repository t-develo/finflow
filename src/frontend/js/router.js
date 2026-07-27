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
  loadingManager.reset();

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
    container.innerHTML = '';
    handler(container, matched ? matched.params : {});
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
