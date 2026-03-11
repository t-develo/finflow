/**
 * dashboard-page.js - Dashboard screen
 *
 * Route: /
 *
 * Features:
 *  - Monthly total and month-over-month change summary cards
 *  - Top category breakdown list with percentage bars
 *  - Recent expenses table (last 5 entries)
 *
 * API (mock in Sprint 1):
 *   GET /api/dashboard/summary
 */

import { mockDashboardApi } from '../mocks/mock-api.js';
import { router } from '../router.js';
import { formatCurrency, formatDate } from '../utils/format.js';

const dashboardApi = mockDashboardApi;

// ---------------------------------------------------------------------------
// Render entry point
// ---------------------------------------------------------------------------

/**
 * @param {HTMLElement} container
 */
export async function renderDashboardPage(container) {
  container.innerHTML = buildShell();

  const contentArea = container.querySelector('#dashboard-content');
  if (!contentArea) return;

  contentArea.innerHTML = `<div class="loading">読み込み中...</div>`;

  try {
    const summary = await dashboardApi.getSummary();
    contentArea.innerHTML = buildHtml(summary);
    attachEventListeners(contentArea);
  } catch {
    contentArea.innerHTML = `
      <div class="alert alert--error">
        データの読み込みに失敗しました。再読み込みしてください。
      </div>`;
  }
}

// ---------------------------------------------------------------------------
// Shell HTML
// ---------------------------------------------------------------------------

function buildShell() {
  return `
    <div class="page-header">
      <h1 class="page-header__title">ダッシュボード</h1>
    </div>
    <div id="dashboard-content">
      <div class="loading">読み込み中...</div>
    </div>
  `;
}

// ---------------------------------------------------------------------------
// Dashboard HTML
// ---------------------------------------------------------------------------

function buildHtml(summary) {
  // Coerce to Number and format to 1 decimal place to guard against
  // non-numeric values being injected into innerHTML via template literals.
  const changeValue = Number(summary.monthOverMonthChange).toFixed(1);
  const isIncrease = Number(summary.monthOverMonthChange) >= 0;
  const changeSign = isIncrease ? '+' : '';
  const changeClass = isIncrease
    ? 'dashboard-card__change--increase'
    : 'dashboard-card__change--decrease';

  return `
    <div class="dashboard-grid">
      <!-- Summary cards -->
      <div class="card dashboard-card">
        <div class="dashboard-card__label">今月の支出</div>
        <div class="dashboard-card__value">${formatCurrency(summary.currentMonthTotal)}</div>
        <div class="dashboard-card__change ${changeClass}">
          前月比 ${changeSign}${changeValue}%
        </div>
      </div>

      <div class="card dashboard-card">
        <div class="dashboard-card__label">先月の支出</div>
        <div class="dashboard-card__value">${formatCurrency(summary.previousMonthTotal)}</div>
      </div>

      <!-- Top categories -->
      <div class="card dashboard-card--wide">
        <h2 class="card__title">カテゴリ別内訳</h2>
        <ul class="category-breakdown" aria-label="カテゴリ別支出内訳">
          ${summary.topCategories.map(buildCategoryRow).join('')}
        </ul>
      </div>

      <!-- Recent expenses -->
      <div class="card dashboard-card--wide">
        <h2 class="card__title">最近の支出</h2>
        ${buildRecentExpensesTable(summary.recentExpenses)}
      </div>
    </div>
  `;
}

function buildCategoryRow(cat) {
  // Coerce percentage to a number to prevent non-numeric values from being
  // embedded into innerHTML via template literals.
  const pct = Number(cat.percentage).toFixed(1);
  const color = escapeHtml(cat.categoryColor || '#6B7280');
  const name = escapeHtml(cat.categoryName || '不明');

  return `
    <li class="category-breakdown__item">
      <span class="category-breakdown__dot"
            style="background-color: ${color};"
            aria-hidden="true"></span>
      <span class="category-breakdown__name">${name}</span>
      <div class="category-breakdown__bar-wrap" role="progressbar"
           aria-valuenow="${pct}" aria-valuemin="0" aria-valuemax="100">
        <div class="category-breakdown__bar"
             style="width: ${pct}%; background-color: ${color};"></div>
      </div>
      <span class="category-breakdown__pct">${pct}%</span>
      <span class="category-breakdown__amount">${formatCurrency(cat.totalAmount)}</span>
    </li>
  `;
}

function buildRecentExpensesTable(expenses) {
  if (!expenses || expenses.length === 0) {
    return `<p class="empty-state__message">最近の支出はありません</p>`;
  }

  const rows = expenses.map(expense => `
    <tr class="table__row"
        data-action="navigate-expense"
        data-id="${expense.id}"
        style="cursor: pointer;"
        tabindex="0"
        aria-label="${escapeHtml(expense.description)} ${formatCurrency(expense.amount)}">
      <td class="table__cell table__cell--muted">${escapeHtml(formatDate(expense.date))}</td>
      <td class="table__cell">${escapeHtml(expense.categoryName || '不明')}</td>
      <td class="table__cell">${escapeHtml(expense.description)}</td>
      <td class="table__cell table__cell--amount">${escapeHtml(formatCurrency(expense.amount))}</td>
    </tr>
  `).join('');

  return `
    <div class="table-container">
      <table class="table" aria-label="最近の支出">
        <thead class="table__head">
          <tr>
            <th class="table__header" scope="col">日付</th>
            <th class="table__header" scope="col">カテゴリ</th>
            <th class="table__header" scope="col">説明</th>
            <th class="table__header table__header--right" scope="col">金額</th>
          </tr>
        </thead>
        <tbody>${rows}</tbody>
      </table>
    </div>
    <div class="dashboard-card__footer">
      <a href="/expenses" class="btn btn--secondary btn--sm" data-navigo>一覧を見る</a>
    </div>
  `;
}

// ---------------------------------------------------------------------------
// Event listeners
// ---------------------------------------------------------------------------

function attachEventListeners(contentArea) {
  contentArea.addEventListener('click', (e) => {
    const row = e.target.closest('[data-action="navigate-expense"]');
    if (!row) return;
    const id = row.getAttribute('data-id');
    if (id) router.navigate(`/expenses/${id}/edit`);
  });

  // Keyboard navigation for expense rows
  contentArea.addEventListener('keydown', (e) => {
    if (e.key !== 'Enter' && e.key !== ' ') return;
    const row = e.target.closest('[data-action="navigate-expense"]');
    if (!row) return;
    e.preventDefault();
    const id = row.getAttribute('data-id');
    if (id) router.navigate(`/expenses/${id}/edit`);
  });
}

// ---------------------------------------------------------------------------
// Utility
// ---------------------------------------------------------------------------

function escapeHtml(text) {
  const div = document.createElement('div');
  div.textContent = String(text ?? '');
  return div.innerHTML;
}
