import { router } from './router.js';
import { auth } from './utils/auth.js';

// Sprint 2: 各ページを直接インポート（Named exports）
import { renderDashboardPage } from './pages/dashboard-page.js';
import { renderCsvImportPage } from './pages/csv-import-page.js';
import { renderSubscriptionPage } from './pages/subscription-page.js';
import { renderCategoryPage } from './pages/category-page.js';

// Sprint 1からの既存ページ（Named exports）
import { renderLoginPage } from './pages/login-page.js';
import { renderRegisterPage } from './pages/register-page.js';
import { renderExpenseListPage } from './pages/expense-list-page.js';
import { renderExpenseFormPage } from './pages/expense-form-page.js';

router
  .on('/login', (container) => {
    renderLoginPage(container);
  })
  .on('/register', (container) => {
    renderRegisterPage(container);
  })
  .on('/dashboard', (container) => {
    renderDashboardPage(container);
  })
  .on('/', (container) => {
    renderDashboardPage(container);
  })
  .on('/expenses', (container) => {
    renderExpenseListPage(container);
  })
  .on('/expenses/import', (container) => {
    renderCsvImportPage(container);
  })
  .on('/subscriptions', (container) => {
    renderSubscriptionPage(container);
  })
  .on('/categories', (container) => {
    renderCategoryPage(container);
  })
  .on('/import', (container) => {
    renderCsvImportPage(container);
  })
  .on('*', (container) => {
    container.innerHTML = '<div class="card"><h2>404 - ページが見つかりません</h2></div>';
  });

// Logout handler
document.getElementById('logout-btn')?.addEventListener('click', () => {
  auth.logout();
  router.navigate('/login');
});

router.start();
