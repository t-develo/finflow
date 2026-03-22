/**
 * subscription-page.js - サブスクリプション管理画面
 *
 * Route: /subscriptions
 *
 * Features:
 *  - サブスクリプション一覧表示
 *  - 新規作成・編集・削除（モーダルフォーム）
 *  - 請求サイクルと次回支払日の表示
 *
 * API:
 *   GET    /api/subscriptions
 *   POST   /api/subscriptions
 *   PUT    /api/subscriptions/:id
 *   DELETE /api/subscriptions/:id
 */

import { api } from '../utils/api-client.js';
import { formatCurrency, formatDate, escapeHtml } from '../utils/format.js';

// ---------------------------------------------------------------------------
// Render entry point
// ---------------------------------------------------------------------------

/**
 * @param {HTMLElement} container
 */
export async function renderSubscriptionPage(container) {
  container.innerHTML = buildShell();

  const contentArea = container.querySelector('#subscription-content');
  if (!contentArea) return;

  await loadAndRender(container, contentArea);
}

// ---------------------------------------------------------------------------
// Shell HTML
// ---------------------------------------------------------------------------

function buildShell() {
  return `
    <div class="page-header">
      <h1 class="page-header__title">サブスクリプション管理</h1>
      <button type="button" class="btn btn--primary" id="add-subscription-btn">
        + 新規追加
      </button>
    </div>
    <div id="subscription-content">
      <div class="loading">読み込み中...</div>
    </div>

    <!-- モーダル -->
    <div id="subscription-modal" class="modal" role="dialog" aria-modal="true" aria-labelledby="modal-title" hidden>
      <div class="modal__backdrop" id="modal-backdrop"></div>
      <div class="modal__dialog">
        <div class="modal__header">
          <h2 class="modal__title" id="modal-title">サブスクリプション</h2>
          <button type="button" class="modal__close" id="modal-close-btn" aria-label="閉じる">×</button>
        </div>
        <div class="modal__body">
          <form id="subscription-form" novalidate>
            <input type="hidden" id="subscription-id">

            <div class="form__group">
              <label class="form__label" for="service-name">サービス名 <span aria-hidden="true">*</span></label>
              <input type="text" id="service-name" class="form__input" required
                     maxlength="200" placeholder="例: Netflix, Spotify" aria-required="true">
              <span class="form__error" id="service-name-error" aria-live="polite"></span>
            </div>

            <div class="form__group">
              <label class="form__label" for="amount">金額 (円) <span aria-hidden="true">*</span></label>
              <input type="number" id="amount" class="form__input" required
                     min="1" step="1" placeholder="例: 980" aria-required="true">
              <span class="form__error" id="amount-error" aria-live="polite"></span>
            </div>

            <div class="form__group">
              <label class="form__label" for="billing-cycle">支払いサイクル</label>
              <select id="billing-cycle" class="form__select">
                <option value="monthly">毎月</option>
                <option value="yearly">毎年</option>
                <option value="weekly">毎週</option>
              </select>
            </div>

            <div class="form__group">
              <label class="form__label" for="next-billing-date">次回支払日 <span aria-hidden="true">*</span></label>
              <input type="date" id="next-billing-date" class="form__input" required aria-required="true">
              <span class="form__error" id="next-billing-date-error" aria-live="polite"></span>
            </div>

            <div class="form__group">
              <label class="form__label" for="notes">メモ</label>
              <textarea id="notes" class="form__textarea" rows="2" maxlength="500"
                        placeholder="任意のメモを入力"></textarea>
            </div>

            <div class="form__group form__group--checkbox">
              <label class="form__label form__label--checkbox">
                <input type="checkbox" id="is-active" checked>
                有効
              </label>
            </div>
          </form>
        </div>
        <div class="modal__footer">
          <button type="button" class="btn btn--secondary" id="modal-cancel-btn">キャンセル</button>
          <button type="button" class="btn btn--primary" id="modal-save-btn">保存</button>
          <button type="button" class="btn btn--danger" id="modal-delete-btn" hidden>削除</button>
        </div>
      </div>
    </div>
  `;
}

// ---------------------------------------------------------------------------
// Data loading and rendering
// ---------------------------------------------------------------------------

async function loadAndRender(container, contentArea) {
  contentArea.innerHTML = `<div class="loading">読み込み中...</div>`;

  // Clean up previous ESC key listener to prevent memory leaks
  if (container._boundHandleEscKey) {
    document.removeEventListener('keydown', container._boundHandleEscKey);
    container._boundHandleEscKey = null;
  }

  try {
    const subscriptions = await api.get('/subscriptions');
    contentArea.innerHTML = buildListHtml(subscriptions);
    attachEventListeners(container, subscriptions);
  } catch {
    contentArea.innerHTML = `
      <div class="alert alert--error">
        データの読み込みに失敗しました。再読み込みしてください。
      </div>`;
  }
}

// ---------------------------------------------------------------------------
// List HTML
// ---------------------------------------------------------------------------

function buildListHtml(subscriptions) {
  if (!subscriptions || subscriptions.length === 0) {
    return `
      <div class="empty-state">
        <p class="empty-state__message">サブスクリプションが登録されていません</p>
      </div>
    `;
  }

  const totalMonthly = subscriptions
    .filter(s => s.isActive)
    .reduce((sum, s) => {
      if (s.billingCycle === 'yearly') return sum + s.amount / 12;
      if (s.billingCycle === 'weekly') return sum + s.amount * 4.33;
      return sum + s.amount;
    }, 0);

  const rows = subscriptions.map(buildSubscriptionRow).join('');

  return `
    <div class="subscription-summary">
      <div class="card subscription-summary__card">
        <div class="subscription-summary__label">月額合計（有効なもの）</div>
        <div class="subscription-summary__value">${formatCurrency(Math.round(totalMonthly))}</div>
      </div>
    </div>
    <div class="table-container">
      <table class="table" aria-label="サブスクリプション一覧">
        <thead class="table__head">
          <tr>
            <th class="table__header" scope="col">サービス名</th>
            <th class="table__header table__header--right" scope="col">金額</th>
            <th class="table__header" scope="col">サイクル</th>
            <th class="table__header" scope="col">次回支払日</th>
            <th class="table__header" scope="col">状態</th>
            <th class="table__header" scope="col">操作</th>
          </tr>
        </thead>
        <tbody>${rows}</tbody>
      </table>
    </div>
  `;
}

function buildSubscriptionRow(sub) {
  const cycleLabel = { monthly: '毎月', yearly: '毎年', weekly: '毎週' }[sub.billingCycle] ?? sub.billingCycle;
  const isActiveLabel = sub.isActive ? '有効' : '無効';
  const statusClass = sub.isActive ? 'badge badge--success' : 'badge badge--muted';
  const today = new Date();
  const billingDate = new Date(sub.nextBillingDate);
  const daysUntil = Math.ceil((billingDate - today) / (1000 * 60 * 60 * 24));
  const isDueSoon = sub.isActive && daysUntil >= 0 && daysUntil <= 3;
  const rowClass = isDueSoon ? 'table__row table__row--warning' : 'table__row';

  return `
    <tr class="${rowClass}" data-id="${sub.id}">
      <td class="table__cell">
        ${escapeHtml(sub.serviceName)}
        ${isDueSoon ? `<span class="badge badge--warning" aria-label="支払い期日が${daysUntil}日後">まもなく</span>` : ''}
      </td>
      <td class="table__cell table__cell--amount">${escapeHtml(formatCurrency(sub.amount))}</td>
      <td class="table__cell">${escapeHtml(cycleLabel)}</td>
      <td class="table__cell">${escapeHtml(formatDate(sub.nextBillingDate))}</td>
      <td class="table__cell"><span class="${statusClass}">${isActiveLabel}</span></td>
      <td class="table__cell">
        <button type="button"
                class="btn btn--ghost btn--sm"
                data-action="edit"
                data-id="${sub.id}"
                aria-label="${escapeHtml(sub.serviceName)}を編集">
          編集
        </button>
      </td>
    </tr>
  `;
}

// ---------------------------------------------------------------------------
// Event listeners
// ---------------------------------------------------------------------------

/**
 * @param {HTMLElement} container
 * @param {Array} subscriptions
 */
function attachEventListeners(container, subscriptions) {
  const modal = container.querySelector('#subscription-modal');

  // 新規追加ボタン
  container.querySelector('#add-subscription-btn')?.addEventListener('click', () => {
    openModal(container, null);
  });

  // 編集ボタン（テーブル行）
  container.querySelector('#subscription-content')?.addEventListener('click', (e) => {
    const editBtn = e.target.closest('[data-action="edit"]');
    if (!editBtn) return;

    const id = parseInt(editBtn.getAttribute('data-id'), 10);
    const sub = subscriptions.find(s => s.id === id);
    if (sub) openModal(container, sub);
  });

  // モーダルを閉じる
  container.querySelector('#modal-close-btn')?.addEventListener('click', () => closeModal(container));
  container.querySelector('#modal-cancel-btn')?.addEventListener('click', () => closeModal(container));
  container.querySelector('#modal-backdrop')?.addEventListener('click', () => closeModal(container));

  // ESCキーでモーダルを閉じる（参照を保持して解除可能にする）
  const boundHandleEscKey = (e) => handleEscKey(container, e);
  container._boundHandleEscKey = boundHandleEscKey;
  document.addEventListener('keydown', boundHandleEscKey);

  // 保存
  container.querySelector('#modal-save-btn')?.addEventListener('click', () => handleSave(container));

  // 削除
  container.querySelector('#modal-delete-btn')?.addEventListener('click', () => handleDelete(container));
}

function handleEscKey(container, e) {
  if (e.key === 'Escape') closeModal(container);
}

// ---------------------------------------------------------------------------
// Modal
// ---------------------------------------------------------------------------

function openModal(container, subscription) {
  const modal = container.querySelector('#subscription-modal');
  const title = container.querySelector('#modal-title');
  const deleteBtn = container.querySelector('#modal-delete-btn');
  const idInput = container.querySelector('#subscription-id');

  if (subscription) {
    title.textContent = 'サブスクリプションを編集';
    deleteBtn.hidden = false;
    idInput.value = subscription.id;

    container.querySelector('#service-name').value = subscription.serviceName ?? '';
    container.querySelector('#amount').value = subscription.amount ?? '';
    container.querySelector('#billing-cycle').value = subscription.billingCycle ?? 'monthly';
    container.querySelector('#next-billing-date').value = subscription.nextBillingDate ?? '';
    container.querySelector('#notes').value = subscription.notes ?? '';
    container.querySelector('#is-active').checked = subscription.isActive ?? true;
  } else {
    title.textContent = 'サブスクリプションを追加';
    deleteBtn.hidden = true;
    idInput.value = '';
    clearForm(container);
  }

  clearValidationErrors(container);
  modal.hidden = false;

  // フォーカスをサービス名フィールドに移動
  container.querySelector('#service-name')?.focus();
}

function closeModal(container) {
  const modal = container.querySelector('#subscription-modal');
  modal.hidden = true;

  // フォーカスを追加ボタンに戻す
  container.querySelector('#add-subscription-btn')?.focus();
}

function clearForm(container) {
  const today = new Date();
  const nextMonth = new Date(today.getFullYear(), today.getMonth() + 1, today.getDate());
  const nextMonthStr = nextMonth.toISOString().split('T')[0];

  container.querySelector('#service-name').value = '';
  container.querySelector('#amount').value = '';
  container.querySelector('#billing-cycle').value = 'monthly';
  container.querySelector('#next-billing-date').value = nextMonthStr;
  container.querySelector('#notes').value = '';
  container.querySelector('#is-active').checked = true;
}

// ---------------------------------------------------------------------------
// Save / Delete
// ---------------------------------------------------------------------------

async function handleSave(container) {
  clearValidationErrors(container);

  const id = container.querySelector('#subscription-id').value;
  const serviceName = container.querySelector('#service-name').value.trim();
  const amount = container.querySelector('#amount').value;
  const billingCycle = container.querySelector('#billing-cycle').value;
  const nextBillingDate = container.querySelector('#next-billing-date').value;
  const notes = container.querySelector('#notes').value.trim();
  const isActive = container.querySelector('#is-active').checked;

  // バリデーション
  let hasError = false;

  if (!serviceName) {
    showFieldError(container, 'service-name-error', 'サービス名を入力してください。');
    hasError = true;
  }

  const amountNum = Number(amount);
  if (!amount || isNaN(amountNum) || amountNum <= 0) {
    showFieldError(container, 'amount-error', '金額は1円以上の数値を入力してください。');
    hasError = true;
  }

  if (!nextBillingDate) {
    showFieldError(container, 'next-billing-date-error', '次回支払日を入力してください。');
    hasError = true;
  }

  if (hasError) return;

  const saveBtn = container.querySelector('#modal-save-btn');
  saveBtn.disabled = true;
  saveBtn.textContent = '保存中...';

  try {
    const payload = { serviceName, amount: amountNum, billingCycle, nextBillingDate, notes, isActive };

    if (id) {
      await api.put(`/subscriptions/${id}`, payload);
    } else {
      await api.post('/subscriptions', payload);
    }

    closeModal(container);
    await loadAndRender(container, container.querySelector('#subscription-content'));
  } catch (err) {
    showFieldError(container, 'service-name-error', err.message ?? '保存に失敗しました。');
  } finally {
    saveBtn.disabled = false;
    saveBtn.textContent = '保存';
  }
}

async function handleDelete(container) {
  const id = container.querySelector('#subscription-id').value;
  if (!id) return;

  const serviceName = container.querySelector('#service-name').value;

  // カスタム確認ダイアログ（alert/confirmは使用禁止）
  const confirmed = await showDeleteConfirm(container, serviceName);
  if (!confirmed) return;

  const deleteBtn = container.querySelector('#modal-delete-btn');
  deleteBtn.disabled = true;

  try {
    await api.delete(`/subscriptions/${id}`);
    closeModal(container);
    await loadAndRender(container, container.querySelector('#subscription-content'));
  } catch (err) {
    showFieldError(container, 'service-name-error', err.message ?? '削除に失敗しました。');
    deleteBtn.disabled = false;
  }
}

/**
 * 削除確認をインラインダイアログで実装（alert/confirm禁止のため）
 * @returns {Promise<boolean>}
 */
function showDeleteConfirm(container, serviceName) {
  return new Promise((resolve) => {
    const confirmArea = container.querySelector('.modal__footer');
    const originalContent = confirmArea.innerHTML;

    confirmArea.innerHTML = `
      <p class="modal__confirm-text">「${escapeHtml(serviceName)}」を削除してよろしいですか？</p>
      <button type="button" class="btn btn--secondary" id="confirm-cancel">キャンセル</button>
      <button type="button" class="btn btn--danger" id="confirm-ok">削除する</button>
    `;

    confirmArea.querySelector('#confirm-cancel').addEventListener('click', () => {
      confirmArea.innerHTML = originalContent;
      resolve(false);
    });

    confirmArea.querySelector('#confirm-ok').addEventListener('click', () => {
      confirmArea.innerHTML = originalContent;
      resolve(true);
    });
  });
}

// ---------------------------------------------------------------------------
// Validation helpers
// ---------------------------------------------------------------------------

function showFieldError(container, errorId, message) {
  const errorEl = container.querySelector(`#${errorId}`);
  if (errorEl) {
    errorEl.textContent = message;
    const input = errorEl.previousElementSibling;
    input?.classList.add('form__input--error');
  }
}

function clearValidationErrors(container) {
  container.querySelectorAll('.form__error').forEach(el => (el.textContent = ''));
  container.querySelectorAll('.form__input--error').forEach(el => el.classList.remove('form__input--error'));
}

// escapeHtml imported from format.js
