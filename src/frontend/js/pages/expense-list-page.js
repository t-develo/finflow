/**
 * expense-list-page.js - Expense list screen
 *
 * Route: /expenses
 *
 * Features:
 *  - Table with date, category badge, description, amount, actions
 *  - Year/month filter (default: current month) and category filter
 *  - Pagination (20 items per page)
 *  - Edit → navigates to /expenses/:id/edit
 *  - Delete → confirmation dialog → DELETE request → row removed
 *  - Total amount and count summary
 *  - Empty state message when no data
 *
 * API:
 *   GET    /api/expenses?year=&month=&categoryId=&page=&pageSize=
 *   GET    /api/categories
 *   DELETE /api/expenses/:id
 */

import { api } from '../utils/api-client.js';
import { router } from '../router.js';
import { toast } from '../components/ff-toast.js';
import { confirmDialog } from '../components/ff-confirm-dialog.js';
import { formatCurrency, formatDate, currentYearMonth, parseYearMonth } from '../utils/format.js';

/**
 * Thin adapters keeping the same call signatures as the former mock API.
 * All requests are delegated to the real api-client.
 */
const expensesApi = {
  getList: ({ year, month, categoryId, page, pageSize }) => {
    const params = new URLSearchParams({ year, month, page, pageSize });
    if (categoryId && String(categoryId) !== '0') params.set('categoryId', categoryId);
    return api.get(`/expenses?${params}`);
  },
  getById: (id) => api.get(`/expenses/${id}`),
  create: (payload) => api.post('/expenses', payload),
  update: (id, payload) => api.put(`/expenses/${id}`, payload),
  remove: (id) => api.delete(`/expenses/${id}`),
};

const categoriesApi = {
  getAll: () => api.get('/categories'),
};

const PAGE_SIZE = 20;

// ---------------------------------------------------------------------------
// Page state
// ---------------------------------------------------------------------------

function createInitialState() {
  return {
    yearMonth: currentYearMonth(),
    categoryId: '0',
    page: 1,
    categories: [],
    listResult: null,
    isLoading: false,
  };
}

// ---------------------------------------------------------------------------
// Render entry point
// ---------------------------------------------------------------------------

/**
 * @param {HTMLElement} container
 */
export async function renderExpenseListPage(container) {
  const state = createInitialState();

  // Initial render with skeleton layout
  container.innerHTML = buildShell();

  // Load categories for filter dropdown
  try {
    state.categories = await categoriesApi.getAll();
    renderCategoryFilter(container, state.categories, state.categoryId);
  } catch {
    // Non-fatal: category filter will be empty
  }

  // Set filter defaults
  const yearMonthInput = container.querySelector('#filter-year-month');
  if (yearMonthInput) yearMonthInput.value = state.yearMonth;

  // Attach event listeners before first data load
  attachEventListeners(container, state);

  // Initial data load
  await loadAndRender(container, state);
}

// ---------------------------------------------------------------------------
// Shell HTML (structure that persists across data refreshes)
// ---------------------------------------------------------------------------

function buildShell() {
  return `
    <div class="page-header">
      <h1 class="page-header__title">支出一覧</h1>
      <a href="/expenses/new" class="btn btn--primary" data-navigo>
        + 支出を追加
      </a>
    </div>

    <!-- Filter bar -->
    <div class="filter-bar">
      <div class="filter-bar__item">
        <label class="filter-bar__label" for="filter-year-month">期間:</label>
        <input
          class="filter-bar__select"
          type="month"
          id="filter-year-month"
          style="padding: 4px 8px;"
        >
      </div>
      <div class="filter-bar__item">
        <label class="filter-bar__label" for="filter-category">カテゴリ:</label>
        <select class="filter-bar__select" id="filter-category">
          <option value="0">全て</option>
        </select>
      </div>
    </div>

    <!-- Data area (replaced on each load) -->
    <div id="expense-list-data">
      <div class="loading">読み込み中...</div>
    </div>
  `;
}

// ---------------------------------------------------------------------------
// Data load and re-render
// ---------------------------------------------------------------------------

async function loadAndRender(container, state) {
  if (state.isLoading) return;
  state.isLoading = true;

  const dataArea = container.querySelector('#expense-list-data');
  if (!dataArea) return;

  dataArea.innerHTML = `<div class="loading">読み込み中...</div>`;

  try {
    const { year, month } = parseYearMonth(state.yearMonth);
    state.listResult = await expensesApi.getList({
      year,
      month,
      categoryId: state.categoryId,
      page: state.page,
      pageSize: PAGE_SIZE,
    });
    dataArea.innerHTML = buildDataHtml(state.listResult);
    attachDataAreaListeners(dataArea, container, state);
  } catch (err) {
    dataArea.innerHTML = `
      <div class="alert alert--error">
        データの読み込みに失敗しました。再読み込みしてください。
      </div>`;
  } finally {
    state.isLoading = false;
  }
}

// ---------------------------------------------------------------------------
// Data area HTML
// ---------------------------------------------------------------------------

function buildDataHtml(result) {
  const { items, total, page, totalPages, totalAmount } = result;

  if (items.length === 0) {
    return `
      <div class="card card--no-padding">
        <div class="empty-state">
          <div class="empty-state__icon">📭</div>
          <p class="empty-state__message">この期間の支出はありません</p>
        </div>
      </div>`;
  }

  return `
    <div class="card card--no-padding">
      <div class="table-container">
        <table class="table" aria-label="支出一覧">
          <thead class="table__head">
            <tr>
              <th class="table__header" scope="col">日付</th>
              <th class="table__header" scope="col">カテゴリ</th>
              <th class="table__header" scope="col">説明</th>
              <th class="table__header table__header--right" scope="col">金額</th>
              <th class="table__header table__header--right" scope="col">操作</th>
            </tr>
          </thead>
          <tbody id="expense-table-body">
            ${items.map(buildExpenseRow).join('')}
          </tbody>
        </table>
      </div>

      <div class="table-summary">
        <span class="table-summary__label">合計 ${total}件</span>
        <span class="table-summary__value">${formatCurrency(totalAmount)}</span>
      </div>

      ${totalPages > 1 ? buildPagination(page, totalPages) : ''}
    </div>
  `;
}

function buildExpenseRow(expense) {
  return `
    <tr class="table__row" data-expense-id="${expense.id}">
      <td class="table__cell table__cell--muted" data-label="日付">
        ${escapeHtml(formatDate(expense.date))}
      </td>
      <td class="table__cell" data-label="カテゴリ">
        ${buildCategoryBadge(expense)}
      </td>
      <td class="table__cell" data-label="説明">
        ${escapeHtml(expense.description)}
      </td>
      <td class="table__cell table__cell--amount" data-label="金額">
        ${escapeHtml(formatCurrency(expense.amount))}
      </td>
      <td class="table__cell" data-label="操作">
        <div class="table__actions">
          <button
            class="table__action-btn table__action-btn--primary"
            data-action="edit"
            data-id="${expense.id}"
            aria-label="編集: ${escapeHtml(expense.description)}"
            title="編集"
          >✏️</button>
          <button
            class="table__action-btn table__action-btn--danger"
            data-action="delete"
            data-id="${expense.id}"
            data-description="${escapeHtml(expense.description)}"
            aria-label="削除: ${escapeHtml(expense.description)}"
            title="削除"
          >🗑️</button>
        </div>
      </td>
    </tr>
  `;
}

function buildCategoryBadge(expense) {
  const color = escapeHtml(expense.categoryColor || '#6B7280');
  const name = escapeHtml(expense.categoryName || '不明');
  return `
    <span class="category-badge">
      <span class="category-badge__dot" style="background-color: ${color};" aria-hidden="true"></span>
      ${name}
    </span>
  `;
}

function buildPagination(page, totalPages) {
  return `
    <div class="pagination">
      <button
        class="pagination__btn"
        data-action="prev-page"
        ${page <= 1 ? 'disabled' : ''}
        aria-label="前のページ"
      >&lt; 前へ</button>
      <span class="pagination__info">${page} / ${totalPages}</span>
      <button
        class="pagination__btn"
        data-action="next-page"
        ${page >= totalPages ? 'disabled' : ''}
        aria-label="次のページ"
      >次へ &gt;</button>
    </div>
  `;
}

// ---------------------------------------------------------------------------
// Category filter rendering
// ---------------------------------------------------------------------------

function renderCategoryFilter(container, categories, selectedId) {
  const select = container.querySelector('#filter-category');
  if (!select) return;

  const options = categories.map(cat => {
    const selected = String(cat.id) === String(selectedId) ? ' selected' : '';
    return `<option value="${cat.id}"${selected}>${escapeHtml(cat.name)}</option>`;
  });

  select.innerHTML = `<option value="0">全て</option>${options.join('')}`;
}

// ---------------------------------------------------------------------------
// Event listeners
// ---------------------------------------------------------------------------

function attachEventListeners(container, state) {
  // Year/month filter change
  container.querySelector('#filter-year-month')?.addEventListener('change', (e) => {
    state.yearMonth = e.target.value;
    state.page = 1;
    loadAndRender(container, state);
  });

  // Category filter change
  container.querySelector('#filter-category')?.addEventListener('change', (e) => {
    state.categoryId = e.target.value;
    state.page = 1;
    loadAndRender(container, state);
  });
}

function attachDataAreaListeners(dataArea, container, state) {
  // Event delegation for edit, delete, pagination
  dataArea.addEventListener('click', async (e) => {
    const btn = e.target.closest('[data-action]');
    if (!btn) return;

    const action = btn.getAttribute('data-action');
    const id = btn.getAttribute('data-id');

    switch (action) {
      case 'edit':
        router.navigate(`/expenses/${id}/edit`);
        break;

      case 'delete':
        await handleDelete(btn, container, state);
        break;

      case 'prev-page':
        if (state.page > 1) {
          state.page -= 1;
          await loadAndRender(container, state);
        }
        break;

      case 'next-page':
        if (state.listResult && state.page < state.listResult.totalPages) {
          state.page += 1;
          await loadAndRender(container, state);
        }
        break;
    }
  });
}

async function handleDelete(btn, container, state) {
  const id = btn.getAttribute('data-id');
  const description = btn.getAttribute('data-description') || '選択した支出';

  const confirmed = await confirmDialog.show({
    title: '支出の削除',
    message: `「${description}」を削除しますか？この操作は取り消せません。`,
    confirmLabel: '削除する',
    cancelLabel: 'キャンセル',
    danger: true,
  });

  if (!confirmed) return;

  try {
    await expensesApi.remove(id);
    toast.show('支出を削除しました', 'success');

    // Reload current page (or previous page if the last item was deleted)
    if (state.listResult?.items.length === 1 && state.page > 1) {
      state.page -= 1;
    }
    await loadAndRender(container, state);
  } catch (err) {
    toast.show(err.message || '削除に失敗しました', 'error');
  }
}

// ---------------------------------------------------------------------------
// Utility
// ---------------------------------------------------------------------------

function escapeHtml(text) {
  const div = document.createElement('div');
  div.textContent = String(text ?? '');
  return div.innerHTML;
}
