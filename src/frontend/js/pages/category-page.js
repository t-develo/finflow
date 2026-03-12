/**
 * category-page.js - カテゴリ管理画面
 *
 * Route: /categories
 *
 * Features:
 *  - カテゴリ一覧表示（システム定義 + ユーザー定義）
 *  - 新規作成・編集・削除（インラインフォーム）
 *  - カラーピッカー（HTML5 color input）
 *
 * API:
 *   GET    /api/categories
 *   POST   /api/categories
 *   PUT    /api/categories/:id
 *   DELETE /api/categories/:id
 */

import { api } from '../utils/api-client.js';

// ---------------------------------------------------------------------------
// Render entry point
// ---------------------------------------------------------------------------

/**
 * @param {HTMLElement} container
 */
export async function renderCategoryPage(container) {
  container.innerHTML = buildShell();

  const contentArea = container.querySelector('#category-content');
  if (!contentArea) return;

  await loadAndRender(container, contentArea);
}

// ---------------------------------------------------------------------------
// Shell HTML
// ---------------------------------------------------------------------------

function buildShell() {
  return `
    <div class="page-header">
      <h1 class="page-header__title">カテゴリ管理</h1>
    </div>
    <div id="category-content">
      <div class="loading">読み込み中...</div>
    </div>
  `;
}

// ---------------------------------------------------------------------------
// Data loading
// ---------------------------------------------------------------------------

async function loadAndRender(container, contentArea) {
  contentArea.innerHTML = `<div class="loading">読み込み中...</div>`;

  try {
    const categories = await api.get('/categories');
    contentArea.innerHTML = buildHtml(categories);
    attachEventListeners(container, contentArea, categories);
  } catch {
    contentArea.innerHTML = `
      <div class="alert alert--error">
        データの読み込みに失敗しました。再読み込みしてください。
      </div>`;
  }
}

// ---------------------------------------------------------------------------
// HTML
// ---------------------------------------------------------------------------

function buildHtml(categories) {
  const systemCategories = categories.filter(c => c.isSystem);
  const userCategories = categories.filter(c => !c.isSystem);

  return `
    <!-- 新規追加フォーム -->
    <div class="card category-add-form">
      <h2 class="card__title">新しいカテゴリを追加</h2>
      <form id="add-category-form" class="category-form" novalidate>
        <div class="category-form__fields">
          <div class="form__group">
            <label class="form__label" for="new-category-name">カテゴリ名 <span aria-hidden="true">*</span></label>
            <input type="text" id="new-category-name" class="form__input"
                   required maxlength="100" placeholder="例: 食費, 交通費"
                   aria-required="true">
            <span class="form__error" id="new-category-name-error" aria-live="polite"></span>
          </div>
          <div class="form__group">
            <label class="form__label" for="new-category-color">色</label>
            <div class="category-form__color-wrap">
              <input type="color" id="new-category-color" class="form__color-input"
                     value="#3B82F6" aria-label="カテゴリカラーを選択">
              <span id="new-category-color-preview" class="category-form__color-preview"
                    aria-hidden="true" style="background-color: #3B82F6;"></span>
            </div>
          </div>
          <div class="category-form__actions">
            <button type="submit" class="btn btn--primary">追加</button>
          </div>
        </div>
      </form>
    </div>

    <!-- ユーザー定義カテゴリ一覧 -->
    <div class="card">
      <h2 class="card__title">マイカテゴリ</h2>
      <div id="user-categories-list">
        ${buildUserCategoryList(userCategories)}
      </div>
    </div>

    <!-- システムカテゴリ一覧（読み取り専用） -->
    <div class="card">
      <h2 class="card__title">システムカテゴリ（変更不可）</h2>
      <ul class="category-list" aria-label="システムカテゴリ一覧">
        ${systemCategories.map(cat => `
          <li class="category-list__item category-list__item--readonly">
            <span class="category-list__dot"
                  style="background-color: ${escapeHtml(cat.color)};"
                  aria-hidden="true"></span>
            <span class="category-list__name">${escapeHtml(cat.name)}</span>
          </li>
        `).join('')}
      </ul>
    </div>
  `;
}

function buildUserCategoryList(categories) {
  if (categories.length === 0) {
    return `<p class="empty-state__message">まだカテゴリが登録されていません</p>`;
  }

  const items = categories.map(cat => buildUserCategoryItem(cat)).join('');
  return `<ul class="category-list" aria-label="ユーザー定義カテゴリ一覧">${items}</ul>`;
}

function buildUserCategoryItem(cat) {
  return `
    <li class="category-list__item" data-id="${cat.id}">
      <span class="category-list__dot"
            style="background-color: ${escapeHtml(cat.color)};"
            aria-hidden="true"></span>
      <!-- 閲覧モード -->
      <span class="category-list__name category-list__name--view">${escapeHtml(cat.name)}</span>
      <div class="category-list__actions">
        <button type="button"
                class="btn btn--ghost btn--sm"
                data-action="edit-category"
                data-id="${cat.id}"
                aria-label="${escapeHtml(cat.name)}を編集">
          編集
        </button>
        <button type="button"
                class="btn btn--ghost btn--sm btn--danger-hover"
                data-action="delete-category"
                data-id="${cat.id}"
                aria-label="${escapeHtml(cat.name)}を削除">
          削除
        </button>
      </div>
    </li>
  `;
}

// ---------------------------------------------------------------------------
// Event listeners
// ---------------------------------------------------------------------------

/**
 * @param {HTMLElement} container
 * @param {HTMLElement} contentArea
 * @param {Array} categories
 */
function attachEventListeners(container, contentArea, categories) {
  // 新規追加フォームの送信
  const addForm = contentArea.querySelector('#add-category-form');
  addForm?.addEventListener('submit', (e) => {
    e.preventDefault();
    handleAddCategory(container, contentArea);
  });

  // カラーピッカーのプレビュー更新
  const colorInput = contentArea.querySelector('#new-category-color');
  const colorPreview = contentArea.querySelector('#new-category-color-preview');
  colorInput?.addEventListener('input', () => {
    if (colorPreview) colorPreview.style.backgroundColor = colorInput.value;
  });

  // ユーザーカテゴリリストのイベント委譲
  const userList = contentArea.querySelector('#user-categories-list');
  userList?.addEventListener('click', async (e) => {
    const editBtn = e.target.closest('[data-action="edit-category"]');
    const deleteBtn = e.target.closest('[data-action="delete-category"]');

    if (editBtn) {
      const id = parseInt(editBtn.getAttribute('data-id'), 10);
      const cat = categories.find(c => c.id === id);
      if (cat) showInlineEditForm(userList, cat, container, contentArea);
    }

    if (deleteBtn) {
      const id = parseInt(deleteBtn.getAttribute('data-id'), 10);
      const cat = categories.find(c => c.id === id);
      if (cat) await handleDeleteCategory(container, contentArea, cat);
    }
  });
}

// ---------------------------------------------------------------------------
// Add category
// ---------------------------------------------------------------------------

async function handleAddCategory(container, contentArea) {
  const nameInput = contentArea.querySelector('#new-category-name');
  const colorInput = contentArea.querySelector('#new-category-color');
  const errorEl = contentArea.querySelector('#new-category-name-error');

  errorEl.textContent = '';
  nameInput.classList.remove('form__input--error');

  const name = nameInput.value.trim();
  if (!name) {
    errorEl.textContent = 'カテゴリ名を入力してください。';
    nameInput.classList.add('form__input--error');
    nameInput.focus();
    return;
  }

  const submitBtn = contentArea.querySelector('#add-category-form button[type="submit"]');
  submitBtn.disabled = true;

  try {
    await api.post('/categories', {
      name,
      color: colorInput.value
    });

    await loadAndRender(container, contentArea);
  } catch (err) {
    errorEl.textContent = err.message ?? '追加に失敗しました。';
    nameInput.classList.add('form__input--error');
    submitBtn.disabled = false;
  }
}

// ---------------------------------------------------------------------------
// Inline edit form
// ---------------------------------------------------------------------------

/**
 * リスト項目をインラインの編集フォームに置き換える
 * @param {HTMLElement} userList
 * @param {{ id: number, name: string, color: string }} cat
 * @param {HTMLElement} container
 * @param {HTMLElement} contentArea
 */
function showInlineEditForm(userList, cat, container, contentArea) {
  const listItem = userList.querySelector(`[data-id="${cat.id}"]`);
  if (!listItem) return;

  listItem.innerHTML = `
    <div class="category-edit-form">
      <input type="color" class="form__color-input" id="edit-color-${cat.id}"
             value="${escapeHtml(cat.color)}" aria-label="カラーを選択">
      <input type="text" class="form__input form__input--inline"
             id="edit-name-${cat.id}" value="${escapeHtml(cat.name)}"
             maxlength="100" aria-label="カテゴリ名を編集" required>
      <span class="form__error" id="edit-error-${cat.id}" aria-live="polite"></span>
      <div class="category-edit-form__actions">
        <button type="button" class="btn btn--primary btn--sm"
                data-action="save-edit" data-id="${cat.id}">保存</button>
        <button type="button" class="btn btn--secondary btn--sm"
                data-action="cancel-edit" data-id="${cat.id}">キャンセル</button>
      </div>
    </div>
  `;

  // カラープレビュー更新
  const colorInput = listItem.querySelector(`#edit-color-${cat.id}`);
  colorInput?.addEventListener('input', () => {
    listItem.querySelector('.category-list__dot')?.remove();
  });

  // 保存ボタン
  listItem.querySelector('[data-action="save-edit"]')?.addEventListener('click', async () => {
    const nameVal = listItem.querySelector(`#edit-name-${cat.id}`).value.trim();
    const colorVal = listItem.querySelector(`#edit-color-${cat.id}`).value;
    const errorEl = listItem.querySelector(`#edit-error-${cat.id}`);

    if (!nameVal) {
      errorEl.textContent = 'カテゴリ名を入力してください。';
      return;
    }

    try {
      await api.put(`/categories/${cat.id}`, { name: nameVal, color: colorVal });
      await loadAndRender(container, contentArea);
    } catch (err) {
      errorEl.textContent = err.message ?? '更新に失敗しました。';
    }
  });

  // キャンセルボタン
  listItem.querySelector('[data-action="cancel-edit"]')?.addEventListener('click', async () => {
    await loadAndRender(container, contentArea);
  });

  listItem.querySelector(`#edit-name-${cat.id}`)?.focus();
}

// ---------------------------------------------------------------------------
// Delete category
// ---------------------------------------------------------------------------

async function handleDeleteCategory(container, contentArea, cat) {
  // インライン確認（alert/confirmは使用禁止）
  const userList = contentArea.querySelector('#user-categories-list');
  const listItem = userList?.querySelector(`[data-id="${cat.id}"]`);
  if (!listItem) return;

  const originalHtml = listItem.innerHTML;
  listItem.innerHTML = `
    <span class="category-list__confirm-text">「${escapeHtml(cat.name)}」を削除しますか？</span>
    <button type="button" class="btn btn--danger btn--sm" id="confirm-delete-${cat.id}">削除</button>
    <button type="button" class="btn btn--secondary btn--sm" id="cancel-delete-${cat.id}">キャンセル</button>
  `;

  listItem.querySelector(`#cancel-delete-${cat.id}`)?.addEventListener('click', () => {
    listItem.innerHTML = originalHtml;
    // イベントリスナーを再アタッチするために再レンダリング
    loadAndRender(container, contentArea);
  });

  listItem.querySelector(`#confirm-delete-${cat.id}`)?.addEventListener('click', async () => {
    try {
      await api.delete(`/categories/${cat.id}`);
      await loadAndRender(container, contentArea);
    } catch (err) {
      listItem.innerHTML = originalHtml;
      loadAndRender(container, contentArea);
    }
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
