import { router } from './router.js';
import { auth } from './utils/auth.js';

// Pages (will be implemented by SE-C in Sprint 1)
async function loadPage(name) {
  const module = await import(`./pages/${name}.js`);
  return module.default;
}

router
  .on('/login', async (container) => {
    const page = await loadPage('login');
    page(container);
  })
  .on('/register', async (container) => {
    const page = await loadPage('register');
    page(container);
  })
  .on('/dashboard', async (container) => {
    const page = await loadPage('dashboard');
    page(container);
  })
  .on('/expenses', async (container) => {
    const page = await loadPage('expenses');
    page(container);
  })
  .on('/subscriptions', async (container) => {
    const page = await loadPage('subscriptions');
    page(container);
  })
  .on('/categories', async (container) => {
    const page = await loadPage('categories');
    page(container);
  })
  .on('/import', async (container) => {
    const page = await loadPage('import');
    page(container);
  })
  .on('*', async (container) => {
    container.innerHTML = '<div class="card"><h2>404 - ページが見つかりません</h2></div>';
  });

// Logout handler
document.getElementById('logout-btn')?.addEventListener('click', () => {
  auth.logout();
  router.navigate('/login');
});

router.start();
