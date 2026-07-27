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
 *   GET    /api/expenses?from=&to=&categoryId=&page=&pageSize=
 *     -> { data: Expense[], pagination: { page, pageSize, totalCount, hasNextPage }, totalAmount }
 *   GET    /api/categories
 *   DELETE /api/expenses/:id
 */

import { api } from '../utils/api-client.js';
import { router } from '../router.js';
import { toast } from '../components/ff-toast.js';
import { confirmDialog } from '../components/ff-confirm-dialog.js';
import { formatCurrency, formatDate, currentYearMonth, parseYearMonth, escapeHtml, sanitizeColor } from '../utils/format.js';

/**
 * Thin adapters keeping the same call signatures as the former mock API.
 * All requests are delegated to the real api-client.
 */
const expensesApi = {
  getList: ({ from, to, categoryId, page, pageSize }) => {
    const params = new URLSearchParams({ from, to, page, pageSize });
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

/**
 * Compute the first/last day of a given year/month as "YYYY-MM-DD" strings,
 * for use as the API's `from`/`to` query params (the backend filters by
 * date range, not by year/month).
 * @param {number} year
 * @param {number} month  1-12
 * @returns {{ from: string, to: string }}
 */
function monthToDateRange(year, month) {
  const pad = (n) => String(n).padStart(2, '0');
  const lastDay = new Date(year, month, 0).getDate();
  return {
    from: `${year}-${pad(month)}-01`,
    to: `${year}-${pad(month)}-${pad(lastDay)}`,
  };
}

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
    isDeleting: false,
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

  // Attach event listeners before first data load.
  //
  // #expense-list-data は buildShell() で 1 度だけ生成され、以後 innerHTML
  // だけが差し替わる**永続要素**なので、そこへの委譲リスナーもここで 1 回
  // 貼れば足りる。以前は loadAndRender() から毎回貼り直しており、フィルタ
  // 変更・ページ送り・削除のたびに click ハンドラが積み上がっていた。
  // 削除の場合、N 個のハンドラが同時に confirmDialog.show() を呼び、
  // シングルトンの _resolve が上書きされて古い Promise が永久に解決しない
  // （＝タップしても何も起きない）状態になる。
  attachEventListeners(container, state);
  attachDataAreaListeners(container.querySelector('#expense-list-data'), container, state);

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
      <a href="/expenses/new" class="btn btn--primary page-header__action" data-navigo>
        <span aria-hidden="true">＋</span> 支出を追加
      </a>
    </div>

    <!-- モバイルでは、スクロールしても常に手の届く右下の FAB に置き換える
         （page-header__action は main.css のモバイル指定で非表示になる）。
         サブスク画面と同じ導線に揃えている。 -->
    <a href="/expenses/new" class="fab" id="add-expense-fab" data-navigo aria-label="支出を追加">
      <span class="fab__icon" aria-hidden="true">＋</span>
    </a>

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

  const dataArea = container.querySelector('#expense-list-data');
  // isLoading を立てる前に判定する。逆順だと、この early return で
  // フラグが立ちっぱなしになり、以後の再読み込みが全て無視される。
  if (!dataArea) return;

  state.isLoading = true;
  dataArea.innerHTML = `<div class="loading">読み込み中...</div>`;

  try {
    const { year, month } = parseYearMonth(state.yearMonth);
    const { from, to } = monthToDateRange(year, month);
    state.listResult = await expensesApi.getList({
      from,
      to,
      categoryId: state.categoryId,
      page: state.page,
      pageSize: PAGE_SIZE,
    });
    // ここでリスナーを貼り直さないこと。dataArea は永続要素なので、
    // 委譲リスナーは renderExpenseListPage() で 1 回貼れば十分であり、
    // 貼り直すと呼ばれるたびに重複する（上のコメント参照）。
    dataArea.innerHTML = buildDataHtml(state.listResult);
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
  const items = result.data ?? [];
  const { page = 1, totalCount = 0 } = result.pagination ?? {};
  const totalAmount = result.totalAmount ?? 0;
  const totalPages = totalPagesOf(totalCount);

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
    <div class="expense-total">
      <span class="expense-total__label">合計 ${totalCount}件</span>
      <span class="expense-total__value">${escapeHtml(formatCurrency(totalAmount))}</span>
    </div>

    <ul class="exp-list" id="expense-table-body" aria-label="支出一覧">
      ${items.map(buildExpenseRow).join('')}
    </ul>

    ${totalPages > 1 ? buildPagination(page, totalPages) : ''}
  `;
}

/**
 * Derive total page count from the API's totalCount. `totalPages` is not
 * returned by the API (only totalCount is), since it can always be derived
 * from totalCount and the known PAGE_SIZE.
 * @param {number} totalCount
 * @returns {number}
 */
function totalPagesOf(totalCount) {
  return Math.max(1, Math.ceil(totalCount / PAGE_SIZE));
}

/**
 * 支出 1 件のカード。
 *
 * 以前は 5 列の <table> で、モバイルでは main.css の汎用カード化
 * （.table tr → display:block ＋ data-label から見出しを生成）に頼っていた。
 * ただしそれは「日付: / カテゴリ: / 説明: / 金額: / 操作:」の 5 行すべてに
 * ラベルを出すため、1 件で画面の約半分を使い、iPhone では 2 件しか
 * 見えなかった。支出で真っ先に読みたいのは「何にいくら使ったか」なので、
 * 説明と金額を主役に据え、日付とカテゴリは 1 行の補助情報にまとめる。
 */
function buildExpenseRow(expense) {
  const description = escapeHtml(expense.description ?? '');

  return `
    <li class="exp-card" data-expense-id="${expense.id}">
      <div class="exp-card__body">
        <div class="exp-card__description">${description}</div>
        <div class="exp-card__meta">
          <span class="exp-card__date">${escapeHtml(formatDate(expense.date))}</span>
          ${buildCategoryBadge(expense)}
        </div>
      </div>
      <div class="exp-card__amount">${escapeHtml(formatCurrency(expense.amount))}</div>
      <div class="exp-card__actions">
        <button
          class="table__action-btn table__action-btn--primary"
          data-action="edit"
          data-id="${expense.id}"
          aria-label="編集: ${description}"
          title="編集"
        >✏️</button>
        <button
          class="table__action-btn table__action-btn--danger"
          data-action="delete"
          data-id="${expense.id}"
          data-description="${description}"
          aria-label="削除: ${description}"
          title="削除"
        >🗑️</button>
      </div>
    </li>
  `;
}

function buildCategoryBadge(expense) {
  const color = sanitizeColor(expense.categoryColor);
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

      case 'next-page': {
        const totalCount = state.listResult?.pagination?.totalCount ?? 0;
        if (state.page < totalPagesOf(totalCount)) {
          state.page += 1;
          await loadAndRender(container, state);
        }
        break;
      }
    }
  });
}

async function handleDelete(btn, container, state) {
  // 確認ダイアログはシングルトンで、show() のたびに _resolve が上書きされる。
  // 連打で 2 回開くと 1 個目の Promise が永久に解決せず、await したまま
  // 残り続けるので、開いている間は 2 発目を受け付けない。
  if (state.isDeleting) return;
  state.isDeleting = true;

  try {
    await runDelete(btn, container, state);
  } finally {
    state.isDeleting = false;
  }
}

async function runDelete(btn, container, state) {
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
    if (state.listResult?.data?.length === 1 && state.page > 1) {
      state.page -= 1;
    }
    await loadAndRender(container, state);
  } catch (err) {
    toast.show(err.message || '削除に失敗しました', 'error');
  }
}

// escapeHtml and sanitizeColor imported from format.js
