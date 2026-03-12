/**
 * dashboard-page.js - Dashboard screen (Sprint 2: Real API + Chart.js graphs)
 *
 * Route: /
 *
 * Features:
 *  - Monthly total and month-over-month change summary cards
 *  - Doughnut chart (Chart.js) for category breakdown
 *  - Recent expenses table (last 5 entries)
 *  - PDF report download link
 *
 * API:
 *   GET /api/dashboard/summary
 *   GET /api/reports/by-category?year=YYYY&month=MM
 */

import { api } from '../utils/api-client.js';
import { router } from '../router.js';
import { formatCurrency, formatDate } from '../utils/format.js';

// Chart.js インスタンスの参照（ページ再レンダリング時に破棄するため）
let categoryChart = null;

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

  const now = new Date();
  const year = now.getFullYear();
  const month = now.getMonth() + 1;

  try {
    // ダッシュボードサマリと月次カテゴリ内訳を並行取得
    const [summary, categoryReport] = await Promise.all([
      api.get('/dashboard/summary'),
      api.get(`/reports/by-category?year=${year}&month=${month}`)
    ]);

    contentArea.innerHTML = buildHtml(summary, year, month);
    attachEventListeners(contentArea);

    // Chart.js グラフの描画（非同期で動的インポート）
    await renderCategoryChart(contentArea, categoryReport.categories ?? []);
  } catch (err) {
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

function buildHtml(summary, year, month) {
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
        <div class="dashboard-card__footer">
          <a href="/reports/monthly/pdf?year=${year}&month=${month}"
             class="btn btn--secondary btn--sm"
             id="pdf-download-btn"
             aria-label="${year}年${month}月のPDFレポートをダウンロード">
            PDFレポート
          </a>
        </div>
      </div>

      <!-- Category chart (Chart.js) -->
      <div class="card dashboard-card--wide">
        <h2 class="card__title">カテゴリ別内訳（${year}年${month}月）</h2>
        <div class="dashboard-chart">
          <canvas id="category-chart"
                  aria-label="カテゴリ別支出の円グラフ"
                  role="img"
                  width="320"
                  height="320"></canvas>
          <div id="chart-legend" class="dashboard-chart__legend" aria-label="カテゴリ凡例">
            <!-- Populated by renderCategoryChart -->
          </div>
        </div>
      </div>

      <!-- Recent expenses -->
      <div class="card dashboard-card--wide">
        <h2 class="card__title">最近の支出</h2>
        ${buildRecentExpensesTable(summary.recentExpenses)}
      </div>
    </div>
  `;
}

// ---------------------------------------------------------------------------
// Chart.js グラフ描画
// ---------------------------------------------------------------------------

/**
 * Chart.js をCDN経由で動的インポートしてドーナツグラフを描画する。
 * @param {HTMLElement} contentArea
 * @param {Array} categories
 */
async function renderCategoryChart(contentArea, categories) {
  // Chart.js をグローバル変数として使用（CDN経由でindex.htmlに読み込み済み）
  const Chart = window.Chart;
  if (!Chart) {
    // Chart.js が読み込まれていない場合はリスト表示にフォールバック
    renderCategoryListFallback(contentArea, categories);
    return;
  }

  const canvas = contentArea.querySelector('#category-chart');
  if (!canvas) return;

  // 既存チャートを破棄（ページ再レンダリング対応）
  if (categoryChart) {
    categoryChart.destroy();
    categoryChart = null;
  }

  if (!categories || categories.length === 0) {
    canvas.parentElement.innerHTML = `<p class="empty-state__message">今月のデータがありません</p>`;
    return;
  }

  const labels = categories.map(c => c.categoryName ?? '不明');
  const data = categories.map(c => Number(c.totalAmount));
  const colors = categories.map(c => c.categoryColor ?? '#6B7280');

  categoryChart = new Chart(canvas, {
    type: 'doughnut',
    data: {
      labels,
      datasets: [{
        data,
        backgroundColor: colors,
        borderWidth: 2,
        borderColor: '#ffffff'
      }]
    },
    options: {
      responsive: true,
      maintainAspectRatio: true,
      plugins: {
        legend: {
          display: false // カスタム凡例を使用
        },
        tooltip: {
          callbacks: {
            label: (context) => {
              const value = context.parsed;
              const total = context.dataset.data.reduce((a, b) => a + b, 0);
              const pct = total > 0 ? ((value / total) * 100).toFixed(1) : '0.0';
              return ` ${formatCurrency(value)} (${pct}%)`;
            }
          }
        }
      }
    }
  });

  // カスタム凡例を構築（Screen Reader対応）
  const legendEl = contentArea.querySelector('#chart-legend');
  if (legendEl) {
    legendEl.innerHTML = categories.map(cat => `
      <div class="dashboard-chart__legend-item">
        <span class="dashboard-chart__legend-dot"
              style="background-color: ${escapeHtml(cat.categoryColor ?? '#6B7280')};"
              aria-hidden="true"></span>
        <span class="dashboard-chart__legend-name">${escapeHtml(cat.categoryName ?? '不明')}</span>
        <span class="dashboard-chart__legend-amount">${formatCurrency(cat.totalAmount)}</span>
        <span class="dashboard-chart__legend-pct">${Number(cat.percentage).toFixed(1)}%</span>
      </div>
    `).join('');
  }
}

/**
 * Chart.js が使えない場合のリスト表示フォールバック
 */
function renderCategoryListFallback(contentArea, categories) {
  const chartArea = contentArea.querySelector('.dashboard-chart');
  if (!chartArea) return;

  const items = categories.map(cat => {
    const pct = Number(cat.percentage).toFixed(1);
    const color = escapeHtml(cat.categoryColor ?? '#6B7280');
    const name = escapeHtml(cat.categoryName ?? '不明');
    return `
      <li class="category-breakdown__item">
        <span class="category-breakdown__dot" style="background-color: ${color};" aria-hidden="true"></span>
        <span class="category-breakdown__name">${name}</span>
        <span class="category-breakdown__pct">${pct}%</span>
        <span class="category-breakdown__amount">${formatCurrency(cat.totalAmount)}</span>
      </li>
    `;
  }).join('');

  chartArea.innerHTML = `<ul class="category-breakdown" aria-label="カテゴリ別支出内訳">${items}</ul>`;
}

// ---------------------------------------------------------------------------
// Recent expenses table
// ---------------------------------------------------------------------------

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
        aria-label="${escapeHtml(expense.description ?? '')} ${formatCurrency(expense.amount)}">
      <td class="table__cell table__cell--muted">${escapeHtml(formatDate(expense.date))}</td>
      <td class="table__cell">${escapeHtml(expense.categoryName ?? '不明')}</td>
      <td class="table__cell">${escapeHtml(expense.description ?? '')}</td>
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
    // PDF download
    const pdfBtn = e.target.closest('#pdf-download-btn');
    if (pdfBtn) {
      e.preventDefault();
      const href = pdfBtn.getAttribute('href');
      if (href) window.open(`/api/reports${href.replace('/reports', '')}`, '_blank');
      return;
    }

    // Navigate to expense
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
