import { router } from './router.js';
import { auth } from './utils/auth.js';

// Register ff-toast globally so the singleton is available immediately
import './components/ff-toast.js';

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
  .on('/expenses/new', (container) => {
    renderExpenseFormPage(container, { mode: 'create' }).catch((err) => {
      console.error('[app] Failed to render expense form (create)', err);
    });
  })
  .on('/expenses/:id/edit', (container, params) => {
    renderExpenseFormPage(container, { mode: 'edit', expenseId: params.id }).catch((err) => {
      console.error('[app] Failed to render expense form (edit)', err);
    });
  })
  // renderSubscriptionPage は「次の遷移時に呼ぶ後片付け関数」を返す。
  // router.handleRoute がそれを預かるので、戻り値をそのまま返すこと
  // （ここでブロック本体にして戻り値を捨てると、document に貼った
  //   ESC リスナーがページ離脱後も残り、他画面で例外を投げる）。
  .on('/subscriptions', (container) => renderSubscriptionPage(container))
  .on('/categories', (container) => {
    renderCategoryPage(container);
  })
  .on('/import', (container) => {
    renderCsvImportPage(container);
  })
  .on('*', (container) => {
    container.innerHTML = '<div class="card"><h2>404 - ページが見つかりません</h2></div>';
  });

// ---------------------------------------------------------------------------
// Logout handler
// ---------------------------------------------------------------------------
document.getElementById('logout-btn')?.addEventListener('click', () => {
  auth.logout();
  router.navigate('/login');
});

// ---------------------------------------------------------------------------
// Hamburger menu (mobile sidebar toggle)
// ---------------------------------------------------------------------------
const hamburgerBtn = document.getElementById('hamburger-btn');
const sidebar = document.getElementById('sidebar');
const sidebarOverlay = document.getElementById('sidebar-overlay');

function openSidebar() {
  sidebar?.classList.add('sidebar--mobile-open');
  sidebarOverlay?.classList.add('sidebar-overlay--visible');
  sidebarOverlay?.removeAttribute('aria-hidden');
  hamburgerBtn?.setAttribute('aria-expanded', 'true');
  hamburgerBtn?.setAttribute('aria-label', 'メニューを閉じる');
  hamburgerBtn?.classList.add('hamburger-btn--open');
  // Prevent the page behind the drawer from scrolling while it's open.
  // CSS owner: needs `.body--drawer-open { overflow: hidden; }` (or
  // equivalent) — see hand-off note in the task report.
  document.body.classList.add('body--drawer-open');
}

function closeSidebar() {
  sidebar?.classList.remove('sidebar--mobile-open');
  sidebarOverlay?.classList.remove('sidebar-overlay--visible');
  sidebarOverlay?.setAttribute('aria-hidden', 'true');
  hamburgerBtn?.setAttribute('aria-expanded', 'false');
  hamburgerBtn?.setAttribute('aria-label', 'メニューを開く');
  hamburgerBtn?.classList.remove('hamburger-btn--open');
  document.body.classList.remove('body--drawer-open');
}

hamburgerBtn?.addEventListener('click', () => {
  const isOpen = sidebar?.classList.contains('sidebar--mobile-open');
  if (isOpen) {
    closeSidebar();
  } else {
    openSidebar();
  }
});

// Close sidebar when overlay is tapped
sidebarOverlay?.addEventListener('click', closeSidebar);

// Close sidebar when a nav link is clicked (mobile navigation)
sidebar?.addEventListener('click', (e) => {
  if (e.target.closest('[data-navigo]')) {
    closeSidebar();
  }
});

// Close sidebar on Escape key
document.addEventListener('keydown', (e) => {
  if (e.key === 'Escape' && sidebar?.classList.contains('sidebar--mobile-open')) {
    closeSidebar();
    hamburgerBtn?.focus();
  }
});

// ---------------------------------------------------------------------------
// Show/hide hamburger button based on auth state & route
// ---------------------------------------------------------------------------

/**
 * Update hamburger button visibility.
 * Show only when the user is authenticated (i.e. sidebar is visible).
 * @param {boolean} isAuthPage
 */
function updateHamburgerVisibility(isAuthPage) {
  if (hamburgerBtn) {
    hamburgerBtn.hidden = isAuthPage;
  }
}

// router.js notifies us of every route change (see router.onRouteChange in
// router.js) so we can keep the hamburger button in sync explicitly,
// instead of inferring it indirectly from sidebar class mutations.
router.onRouteChange((isAuthPage) => {
  // ドロワーは「サイドバー内の [data-navigo] リンクのクリック」でしか
  // 閉じていなかった（下の sidebar クリックハンドラ）。そのため
  //   - ログアウトボタン（#logout-btn は [data-navigo] ではない）
  //   - 401 応答による /login へのリダイレクト
  //   - ブラウザの戻る/進む（popstate）
  // のいずれかで遷移すると、.sidebar-overlay--visible が付いたまま残る。
  // このとき overlay は pointer-events:auto の全画面要素なので、
  // 遷移先（多くはログイン画面）のタップを丸ごと吸い込んでしまう。
  // 遷移経路によらず必ず閉じることで、この穴を塞ぐ。
  closeSidebar();
  updateHamburgerVisibility(isAuthPage);
});

router.start();

// index.html のインライン起動ガードに「ここまで到達した」ことを知らせる。
// これが立たないまま一定時間が過ぎると、起動ガードがキャッシュの自己修復を試み、
// それでも駄目ならエラー画面を出す（無言の白画面を防ぐ）。
// router.start() の**後**に置くこと — 初回描画まで含めて成功とみなしたいため。
window.__ffBooted = true;
