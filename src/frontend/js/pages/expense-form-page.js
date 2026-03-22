/**
 * expense-form-page.js - Expense add/edit form screen
 *
 * Routes:
 *   /expenses/new        - Add new expense (mode: 'create')
 *   /expenses/:id/edit   - Edit existing expense (mode: 'edit')
 *
 * Features:
 *  - Form fields: amount, category, date, description, note
 *  - Client-side validation with inline error messages
 *  - Create: POST /api/expenses
 *  - Edit:   GET /api/expenses/:id (prefill) + PUT /api/expenses/:id
 *  - Toast notification on success, redirect to /expenses
 *
 * API:
 *   GET    /api/categories
 *   GET    /api/expenses/:id
 *   POST   /api/expenses
 *   PUT    /api/expenses/:id
 */

import { api } from '../utils/api-client.js';
import { router } from '../router.js';
import { toast } from '../components/ff-toast.js';
import { escapeHtml } from '../utils/format.js';

/**
 * Thin adapters keeping the same call signatures as the former mock API.
 */
const expensesApi = {
  getById: (id) => api.get(`/expenses/${id}`),
  create: (payload) => api.post('/expenses', payload),
  update: (id, payload) => api.put(`/expenses/${id}`, payload),
};

const categoriesApi = {
  getAll: () => api.get('/categories'),
};

// ---------------------------------------------------------------------------
// Render entry point
// ---------------------------------------------------------------------------

/**
 * @param {HTMLElement} container
 * @param {{ mode: 'create' | 'edit', expenseId?: string }} options
 */
export async function renderExpenseFormPage(container, { mode = 'create', expenseId } = {}) {
  const isEdit = mode === 'edit';
  const title = isEdit ? '支出を編集' : '支出を追加';

  container.innerHTML = buildShell(title);

  const formArea = container.querySelector('#expense-form-area');
  if (!formArea) return;

  formArea.innerHTML = `<div class="loading">読み込み中...</div>`;

  let categories = [];
  let expense = null;

  try {
    // Load categories (always needed for the dropdown)
    categories = await categoriesApi.getAll();

    // Load existing expense when editing
    if (isEdit && expenseId) {
      expense = await expensesApi.getById(expenseId);
    }
  } catch (err) {
    formArea.innerHTML = `
      <div class="alert alert--error">
        ${isEdit ? '支出データの読み込みに失敗しました。' : 'カテゴリの読み込みに失敗しました。'}
        再読み込みしてください。
      </div>`;
    return;
  }

  formArea.innerHTML = buildFormHtml({ categories, expense, isEdit });
  attachEventListeners(formArea, { mode, expenseId, categories });
}

// ---------------------------------------------------------------------------
// Shell HTML
// ---------------------------------------------------------------------------

function buildShell(title) {
  return `
    <div class="page-header">
      <h1 class="page-header__title">${escapeHtml(title)}</h1>
      <a href="/expenses" class="btn btn--secondary" data-navigo>
        &lt; 一覧に戻る
      </a>
    </div>
    <div id="expense-form-area">
      <div class="loading">読み込み中...</div>
    </div>
  `;
}

// ---------------------------------------------------------------------------
// Form HTML
// ---------------------------------------------------------------------------

/**
 * @param {{ categories: Array, expense: object|null, isEdit: boolean }} opts
 */
function buildFormHtml({ categories, expense, isEdit }) {
  const today = new Date().toISOString().slice(0, 10);
  const defaultDate = expense?.date ?? today;
  const defaultAmount = expense?.amount ?? '';
  const defaultCategoryId = expense?.categoryId ?? '';
  const defaultDescription = expense?.description ?? '';
  const defaultNote = expense?.note ?? '';

  const categoryOptions = categories.map(cat => {
    const selected = cat.id === defaultCategoryId ? ' selected' : '';
    return `<option value="${cat.id}"${selected}>${escapeHtml(cat.name)}</option>`;
  }).join('');

  return `
    <div class="card">
      <form id="expense-form" novalidate>
        <div class="form-group">
          <label class="form-label" for="field-amount">
            金額 <span class="form-label__required" aria-label="必須">*</span>
          </label>
          <div class="form-input-wrap form-input-wrap--suffix">
            <input
              class="form-input"
              type="number"
              id="field-amount"
              name="amount"
              min="1"
              step="1"
              value="${escapeHtml(String(defaultAmount))}"
              placeholder="0"
              required
              aria-required="true"
              aria-describedby="error-amount"
            >
            <span class="form-input__suffix">円</span>
          </div>
          <p class="form-error" id="error-amount" role="alert" hidden></p>
        </div>

        <div class="form-group">
          <label class="form-label" for="field-category">
            カテゴリ <span class="form-label__required" aria-label="必須">*</span>
          </label>
          <select
            class="form-select"
            id="field-category"
            name="categoryId"
            required
            aria-required="true"
            aria-describedby="error-category"
          >
            <option value="">選択してください</option>
            ${categoryOptions}
          </select>
          <p class="form-error" id="error-category" role="alert" hidden></p>
        </div>

        <div class="form-group">
          <label class="form-label" for="field-date">
            日付 <span class="form-label__required" aria-label="必須">*</span>
          </label>
          <input
            class="form-input"
            type="date"
            id="field-date"
            name="date"
            value="${escapeHtml(defaultDate)}"
            required
            aria-required="true"
            aria-describedby="error-date"
          >
          <p class="form-error" id="error-date" role="alert" hidden></p>
        </div>

        <div class="form-group">
          <label class="form-label" for="field-description">
            説明 <span class="form-label__required" aria-label="必須">*</span>
          </label>
          <input
            class="form-input"
            type="text"
            id="field-description"
            name="description"
            value="${escapeHtml(defaultDescription)}"
            maxlength="200"
            placeholder="例: スーパーマーケット"
            required
            aria-required="true"
            aria-describedby="error-description"
          >
          <p class="form-error" id="error-description" role="alert" hidden></p>
        </div>

        <div class="form-group">
          <label class="form-label" for="field-note">メモ</label>
          <input
            class="form-input"
            type="text"
            id="field-note"
            name="note"
            value="${escapeHtml(defaultNote)}"
            maxlength="500"
            placeholder="任意のメモ"
          >
        </div>

        <div class="form-actions">
          <a href="/expenses" class="btn btn--secondary" data-navigo>キャンセル</a>
          <button type="submit" class="btn btn--primary" id="submit-btn">
            ${isEdit ? '更新する' : '保存する'}
          </button>
        </div>
      </form>
    </div>
  `;
}

// ---------------------------------------------------------------------------
// Event listeners
// ---------------------------------------------------------------------------

/**
 * @param {HTMLElement} formArea
 * @param {{ mode: string, expenseId?: string, categories: Array }} opts
 */
function attachEventListeners(formArea, { mode, expenseId, categories }) {
  const form = formArea.querySelector('#expense-form');
  if (!form) return;

  // Inline validation on blur
  form.addEventListener('focusout', (e) => {
    const field = e.target.closest('[name]');
    if (!field) return;
    validateField(field);
  });

  // Form submission
  form.addEventListener('submit', async (e) => {
    e.preventDefault();
    await handleSubmit(form, { mode, expenseId, categories });
  });
}

// ---------------------------------------------------------------------------
// Validation
// ---------------------------------------------------------------------------

/**
 * Validate a single field and show/hide its error message.
 * @param {HTMLElement} field
 * @returns {boolean} true if valid
 */
function validateField(field) {
  const errorEl = field.closest('.form-group')?.querySelector('.form-error');
  let message = '';

  switch (field.name) {
    case 'amount': {
      const val = Number(field.value);
      if (!field.value) {
        message = '金額を入力してください';
      } else if (isNaN(val) || val <= 0) {
        message = '金額は1以上の数値を入力してください';
      }
      break;
    }
    case 'categoryId':
      if (!field.value) message = 'カテゴリを選択してください';
      break;
    case 'date':
      if (!field.value) message = '日付を入力してください';
      break;
    case 'description':
      if (!field.value.trim()) message = '説明を入力してください';
      break;
  }

  if (errorEl) {
    errorEl.textContent = message;
    if (message) {
      errorEl.removeAttribute('hidden');
      field.classList.add('form-input--error');
    } else {
      errorEl.setAttribute('hidden', '');
      field.classList.remove('form-input--error');
    }
  }

  return !message;
}

/**
 * Validate all required fields and return true when all pass.
 * @param {HTMLFormElement} form
 * @returns {boolean}
 */
function validateForm(form) {
  const fields = form.querySelectorAll('[name="amount"], [name="categoryId"], [name="date"], [name="description"]');
  let isValid = true;
  fields.forEach(field => {
    if (!validateField(field)) isValid = false;
  });
  return isValid;
}

// ---------------------------------------------------------------------------
// Submit handler
// ---------------------------------------------------------------------------

/**
 * @param {HTMLFormElement} form
 * @param {{ mode: string, expenseId?: string, categories: Array }} opts
 */
async function handleSubmit(form, { mode, expenseId }) {
  if (!validateForm(form)) return;

  const submitBtn = form.querySelector('#submit-btn');
  const isEdit = mode === 'edit';

  submitBtn.disabled = true;
  submitBtn.textContent = isEdit ? '更新中...' : '保存中...';

  const payload = {
    amount: Number(form.elements['amount'].value),
    categoryId: Number(form.elements['categoryId'].value),
    date: form.elements['date'].value,
    description: form.elements['description'].value.trim(),
    note: form.elements['note'].value.trim(),
  };

  try {
    if (isEdit) {
      await expensesApi.update(expenseId, payload);
      toast.show('支出を更新しました', 'success');
    } else {
      await expensesApi.create(payload);
      toast.show('支出を追加しました', 'success');
    }
    router.navigate('/expenses');
  } catch (err) {
    toast.show(err.message || '保存に失敗しました', 'error');
    submitBtn.disabled = false;
    submitBtn.textContent = isEdit ? '更新する' : '保存する';
  }
}

// escapeHtml imported from format.js
