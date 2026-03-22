/**
 * csv-import-page.js - CSV取込画面
 *
 * Route: /expenses/import
 *
 * Features:
 *  - ドラッグ&ドロップによるCSVファイル選択
 *  - ファイル選択ボタンによる代替操作
 *  - フォーマット選択（汎用/MUFG/楽天カード）
 *  - アップロード進捗とインポート結果表示
 *
 * API:
 *   POST /api/expenses/import (multipart/form-data)
 */

import { api } from '../utils/api-client.js';
import { router } from '../router.js';
import { escapeHtml } from '../utils/format.js';

const MAX_FILE_SIZE_BYTES = 10 * 1024 * 1024; // 10MB
const ACCEPTED_MIME_TYPES = ['text/csv', 'application/csv', 'text/plain'];

// ---------------------------------------------------------------------------
// Render entry point
// ---------------------------------------------------------------------------

/**
 * @param {HTMLElement} container
 */
export function renderCsvImportPage(container) {
  container.innerHTML = buildHtml();
  attachEventListeners(container);
}

// ---------------------------------------------------------------------------
// HTML
// ---------------------------------------------------------------------------

function buildHtml() {
  return `
    <div class="page-header">
      <h1 class="page-header__title">CSV取込</h1>
      <a href="/expenses" class="btn btn--secondary" data-navigo>戻る</a>
    </div>

    <div class="card csv-import">
      <p class="csv-import__description">
        CSVファイルをドラッグ&amp;ドロップするか、ファイルを選択してアップロードしてください。
      </p>

      <!-- フォーマット選択 -->
      <div class="form__group">
        <label class="form__label" for="bank-format">銀行フォーマット</label>
        <select id="bank-format" class="form__select" aria-label="CSVフォーマットを選択">
          <option value="">自動判定（推奨）</option>
          <option value="generic">汎用フォーマット</option>
          <option value="mufg">三菱UFJ銀行（MUFG）</option>
          <option value="rakuten">楽天カード</option>
        </select>
      </div>

      <!-- ドラッグ&ドロップゾーン -->
      <div id="drop-zone"
           class="csv-import__drop-zone"
           role="button"
           tabindex="0"
           aria-label="CSVファイルをドラッグ&ドロップ、またはクリックしてファイルを選択"
           aria-describedby="drop-zone-hint">
        <div class="csv-import__drop-icon" aria-hidden="true">
          <svg width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
            <path d="M12 16v-8m0 0-3 3m3-3 3 3M6 20h12a2 2 0 002-2V8a2 2 0 00-.586-1.414l-4-4A2 2 0 0012 2H6a2 2 0 00-2 2v14a2 2 0 002 2z"/>
          </svg>
        </div>
        <p class="csv-import__drop-text">CSVファイルをここにドラッグ&amp;ドロップ</p>
        <p id="drop-zone-hint" class="csv-import__drop-hint">または</p>
        <button type="button" class="btn btn--secondary" id="file-select-btn">
          ファイルを選択
        </button>
        <input type="file"
               id="file-input"
               accept=".csv,text/csv"
               class="csv-import__file-input"
               aria-hidden="true"
               tabindex="-1">
      </div>

      <!-- 選択済みファイル情報 -->
      <div id="file-info" class="csv-import__file-info" hidden>
        <span id="file-name" class="csv-import__file-name"></span>
        <span id="file-size" class="csv-import__file-size"></span>
        <button type="button" id="clear-file-btn" class="btn btn--ghost btn--sm" aria-label="ファイルをクリア">
          ×
        </button>
      </div>

      <!-- アップロードボタン -->
      <div class="csv-import__actions">
        <button type="button"
                id="upload-btn"
                class="btn btn--primary"
                disabled
                aria-label="選択したCSVファイルをインポート">
          インポート実行
        </button>
      </div>

      <!-- 進捗表示 -->
      <div id="progress-area" class="csv-import__progress" hidden aria-live="polite">
        <div class="loading">アップロード中...</div>
      </div>

      <!-- 結果表示 -->
      <div id="result-area" aria-live="assertive"></div>
    </div>
  `;
}

// ---------------------------------------------------------------------------
// Event listeners
// ---------------------------------------------------------------------------

/**
 * @param {HTMLElement} container
 */
function attachEventListeners(container) {
  const dropZone = container.querySelector('#drop-zone');
  const fileInput = container.querySelector('#file-input');
  const fileSelectBtn = container.querySelector('#file-select-btn');
  const clearFileBtn = container.querySelector('#clear-file-btn');
  const uploadBtn = container.querySelector('#upload-btn');

  // ファイル選択ボタン
  fileSelectBtn.addEventListener('click', () => fileInput.click());

  // ドロップゾーンのキーボード操作（Enter/Space でファイル選択ダイアログを開く）
  dropZone.addEventListener('keydown', (e) => {
    if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault();
      fileInput.click();
    }
  });

  // ドラッグオーバー時のスタイル変更
  dropZone.addEventListener('dragover', (e) => {
    e.preventDefault();
    dropZone.classList.add('csv-import__drop-zone--active');
  });

  dropZone.addEventListener('dragleave', () => {
    dropZone.classList.remove('csv-import__drop-zone--active');
  });

  // ドロップ処理
  dropZone.addEventListener('drop', (e) => {
    e.preventDefault();
    dropZone.classList.remove('csv-import__drop-zone--active');

    const file = e.dataTransfer?.files?.[0];
    if (file) handleFileSelected(container, file);
  });

  // ファイル入力変更
  fileInput.addEventListener('change', () => {
    const file = fileInput.files?.[0];
    if (file) handleFileSelected(container, file);
  });

  // ファイルクリア
  clearFileBtn.addEventListener('click', () => clearFile(container));

  // アップロード実行
  uploadBtn.addEventListener('click', () => handleUpload(container));
}

// ---------------------------------------------------------------------------
// File handling
// ---------------------------------------------------------------------------

/**
 * ファイルが選択/ドロップされたときの処理
 * @param {HTMLElement} container
 * @param {File} file
 */
function handleFileSelected(container, file) {
  const resultArea = container.querySelector('#result-area');
  resultArea.innerHTML = '';

  // ファイル種類チェック
  const isValidType = ACCEPTED_MIME_TYPES.includes(file.type) ||
    file.name.toLowerCase().endsWith('.csv');
  if (!isValidType) {
    showError(resultArea, 'CSVファイル（.csv）のみアップロード可能です。');
    return;
  }

  // ファイルサイズチェック
  if (file.size > MAX_FILE_SIZE_BYTES) {
    showError(resultArea, `ファイルサイズは10MB以下にしてください（現在: ${formatFileSize(file.size)}）。`);
    return;
  }

  // ファイル情報を表示
  const fileInfo = container.querySelector('#file-info');
  const fileNameEl = container.querySelector('#file-name');
  const fileSizeEl = container.querySelector('#file-size');
  const uploadBtn = container.querySelector('#upload-btn');

  fileNameEl.textContent = file.name;
  fileSizeEl.textContent = `(${formatFileSize(file.size)})`;
  fileInfo.hidden = false;
  uploadBtn.disabled = false;

  // ファイルオブジェクトをDOMに保持（アップロード時に参照）
  container.dataset.selectedFile = '';
  container._selectedFile = file;
}

/**
 * ファイル選択をクリアする
 */
function clearFile(container) {
  container._selectedFile = null;

  const fileInfo = container.querySelector('#file-info');
  const fileInput = container.querySelector('#file-input');
  const uploadBtn = container.querySelector('#upload-btn');
  const resultArea = container.querySelector('#result-area');

  fileInfo.hidden = true;
  fileInput.value = '';
  uploadBtn.disabled = true;
  resultArea.innerHTML = '';
}

// ---------------------------------------------------------------------------
// Upload
// ---------------------------------------------------------------------------

async function handleUpload(container) {
  const file = container._selectedFile;
  if (!file) return;

  const bankFormat = container.querySelector('#bank-format')?.value ?? '';
  const uploadBtn = container.querySelector('#upload-btn');
  const progressArea = container.querySelector('#progress-area');
  const resultArea = container.querySelector('#result-area');

  // 二重送信防止
  uploadBtn.disabled = true;
  progressArea.hidden = false;
  resultArea.innerHTML = '';

  try {
    const formData = new FormData();
    formData.append('file', file);

    const path = bankFormat
      ? `/expenses/import?bankFormat=${encodeURIComponent(bankFormat)}`
      : '/expenses/import';

    const result = await api.uploadFile(path, formData);
    showImportResult(resultArea, result);
  } catch (err) {
    showError(resultArea, err.message ?? 'インポートに失敗しました。');
  } finally {
    progressArea.hidden = true;
    uploadBtn.disabled = false;
  }
}

// ---------------------------------------------------------------------------
// Result display
// ---------------------------------------------------------------------------

/**
 * インポート結果を表示する
 * @param {HTMLElement} resultArea
 * @param {{imported: number, skipped: number, errors: string[]}} result
 */
function showImportResult(resultArea, result) {
  const hasErrors = result.errors && result.errors.length > 0;
  const alertClass = hasErrors ? 'alert--warning' : 'alert--success';

  const errorsHtml = hasErrors
    ? `
      <details class="csv-import__error-details">
        <summary>${result.errors.length}件のエラー</summary>
        <ul class="csv-import__error-list">
          ${result.errors.map(e => `<li>${escapeHtml(e)}</li>`).join('')}
        </ul>
      </details>
    `
    : '';

  resultArea.innerHTML = `
    <div class="alert ${alertClass}" role="status">
      <strong>インポート完了</strong><br>
      取込成功: ${result.imported}件 ／ スキップ: ${result.skipped}件
      ${errorsHtml}
      <div class="csv-import__result-actions">
        <a href="/expenses" class="btn btn--primary btn--sm" data-navigo>
          支出一覧を確認
        </a>
      </div>
    </div>
  `;
}

function showError(resultArea, message) {
  resultArea.innerHTML = `
    <div class="alert alert--error" role="alert">
      ${escapeHtml(message)}
    </div>
  `;
}

// ---------------------------------------------------------------------------
// Utility
// ---------------------------------------------------------------------------

function formatFileSize(bytes) {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

// escapeHtml imported from format.js
