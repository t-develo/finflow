import { auth } from './utils/auth.js';

const routes = new Map();

function navigate(path) {
  window.history.pushState({}, '', path);
  handleRoute(path);
}

function handleRoute(path) {
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

  const handler = routes.get(path) || routes.get('*');
  if (handler) {
    container.innerHTML = '';
    handler(container);
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
    routes.set(path, handler);
    return this;
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
