/**
 * subscription-page.js - サブスクリプション管理画面
 *
 * Route: /subscriptions
 *
 * Features:
 *  - サブスクリプション一覧表示（モバイルはカード、デスクトップは表形式）
 *  - 新規作成・編集・削除（一覧行から直接削除できる）
 *  - 請求サイクルと次回支払日の表示
 *
 * API:
 *   GET    /api/subscriptions
 *   POST   /api/subscriptions
 *   PUT    /api/subscriptions/:id
 *   DELETE /api/subscriptions/:id
 *
 * ---------------------------------------------------------------------------
 * イベントリスナーの設計（過去に二重登録バグを起こした箇所なので必読）
 * ---------------------------------------------------------------------------
 * この画面の DOM は 2 層に分かれている。
 *
 *   shell  … buildShell() が作る。ページ描画時に **1 回だけ** 生成され、
 *            以後 loadAndRender() では作り直されない。
 *            (#add-subscription-btn / #subscription-content 自体 /
 *             #subscription-modal 一式 / #modal-save-btn / #modal-delete-btn …)
 *   list   … buildListHtml() が作る。#subscription-content の **中身** で、
 *            データ再取得のたびに innerHTML ごと差し替わる。
 *
 * 修正前は loadAndRender() が毎回 attachEventListeners() を呼び、その中で
 * **shell 側の永続要素**にリスナーを貼り直していた。要素が作り直されない以上
 * 古いリスナーは死なないので、保存のたびに #modal-save-btn のリスナーが増え、
 * さらに増えたリスナーがそれぞれ loadAndRender() を呼ぶため 1→2→4→8 と
 * 指数的に増殖した。結果、2 件目の登録で POST が 2 回、3 件目で 4 回飛び、
 * 「二重・三重に登録される」という症状になっていた。
 *
 * なお saveBtn.disabled = true では防げない。1 回のクリックのディスパッチは、
 * 途中で disabled にしても**その要素に登録済みの全リスナーを最後まで呼ぶ**ため。
 *
 * したがって規約は次の 2 点:
 *   1. shell 側へのリスナーは attachShellListeners() で **1 回だけ** 貼る。
 *      loadAndRender() からは絶対に呼ばない。
 *   2. list 側は #subscription-content への **イベント委譲** で扱う
 *      （委譲元は永続要素なので、中身が何度差し替わってもリスナーは 1 本）。
 * さらに保険として state.busy による多重送信ガードを置く。
 */

import { api } from '../utils/api-client.js';
import { confirmDialog } from '../components/ff-confirm-dialog.js';
import { toast } from '../components/ff-toast.js';
import { formatCurrency, formatDate, escapeHtml } from '../utils/format.js';

const CYCLE_LABELS = { monthly: '毎月', yearly: '毎年', weekly: '毎週' };

/** 「まもなく」バッジを出す閾値（日）。NotificationScheduler の 3 日と揃えている。 */
const DUE_SOON_DAYS = 3;

// ---------------------------------------------------------------------------
// Render entry point
// ---------------------------------------------------------------------------

/**
 * サブスク画面を描画する。
 *
 * 同期関数である点が重要。router は戻り値が関数なら「このページの後片付け」
 * として預かるため、async にすると Promise が返って後片付けが登録されない。
 * データ取得は内部で非同期に走らせる。
 *
 * @param {HTMLElement} container
 * @returns {() => void} 後片付け関数（router が次の遷移直前に呼ぶ）
 */
export function renderSubscriptionPage(container) {
  container.innerHTML = buildShell();

  /**
   * ページ内で共有する状態。
   * subscriptions を state に持たせるのは、編集ハンドラが
   * 「attachEventListeners の引数クロージャ」ではなく常に最新の一覧を
   * 参照できるようにするため（リスナーを貼り直さずに済ませる前提条件）。
   */
  const state = {
    subscriptions: [],
    busy: false,
    escHandler: null,
  };

  attachShellListeners(container, state);

  loadAndRender(container, state).catch((err) => {
    console.error('[subscription-page] initial load failed', err);
  });

  return () => teardown(state);
}

/**
 * ページを離れるときの後片付け。
 *
 * ESC ハンドラは document に貼るため、#page-container を空にしても残る。
 * 残ったまま他の画面で ESC を押すと closeModal() が存在しないモーダルを
 * 触って TypeError になる（実際に踏んだ）。
 */
function teardown(state) {
  if (state.escHandler) {
    document.removeEventListener('keydown', state.escHandler);
    state.escHandler = null;
  }
}

// ---------------------------------------------------------------------------
// Shell HTML
// ---------------------------------------------------------------------------

function buildShell() {
  return `
    <div class="page-header">
      <h1 class="page-header__title">サブスクリプション管理</h1>
      <button type="button" class="btn btn--primary page-header__action" id="add-subscription-btn">
        <span aria-hidden="true">＋</span> 新規追加
      </button>
    </div>
    <div id="subscription-content">
      <div class="loading">読み込み中...</div>
    </div>

    <!-- モバイルで常に手の届く位置に置く追加ボタン（デスクトップでは非表示） -->
    <button type="button" class="fab" id="add-subscription-fab" aria-label="サブスクリプションを追加">
      <span class="fab__icon" aria-hidden="true">＋</span>
    </button>

    <!-- モーダル（モバイルではボトムシートとして表示される） -->
    <div id="subscription-modal" class="modal" role="dialog" aria-modal="true" aria-labelledby="modal-title" hidden>
      <div class="modal__backdrop" id="modal-backdrop"></div>
      <div class="modal__dialog">
        <div class="modal__grabber" aria-hidden="true"></div>
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
                     min="1" step="1" inputmode="numeric" placeholder="例: 980" aria-required="true">
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
          <button type="button" class="btn btn--primary btn--block" id="modal-save-btn">保存</button>
          <button type="button" class="btn btn--secondary btn--block" id="modal-cancel-btn">キャンセル</button>
          <button type="button" class="btn btn--danger-text btn--block" id="modal-delete-btn" hidden>削除する</button>
        </div>
      </div>
    </div>
  `;
}

// ---------------------------------------------------------------------------
// Data loading and rendering
// ---------------------------------------------------------------------------

/**
 * 一覧を取得して #subscription-content の中身だけを差し替える。
 *
 * **この関数からリスナーを貼ってはいけない。** 貼り先の #subscription-content /
 * モーダルは shell 側の永続要素なので、呼ばれるたびにリスナーが積み上がる
 * （ファイル冒頭の設計メモ参照）。
 */
async function loadAndRender(container, state) {
  const contentArea = container.querySelector('#subscription-content');
  if (!contentArea) return;

  contentArea.innerHTML = `<div class="loading">読み込み中...</div>`;

  try {
    const subscriptions = await api.get('/subscriptions');
    state.subscriptions = Array.isArray(subscriptions) ? subscriptions : [];
    contentArea.innerHTML = buildListHtml(state.subscriptions);
  } catch (err) {
    state.subscriptions = [];
    contentArea.innerHTML = `
      <div class="alert alert--error">
        データの読み込みに失敗しました。再読み込みしてください。
      </div>`;
    console.error('[subscription-page] load failed', err);
  }
}

// ---------------------------------------------------------------------------
// List HTML
// ---------------------------------------------------------------------------

function buildListHtml(subscriptions) {
  if (!subscriptions || subscriptions.length === 0) {
    return `
      <div class="empty-state">
        <span class="empty-state__icon" aria-hidden="true">🗓️</span>
        <p class="empty-state__message">サブスクリプションが登録されていません</p>
        <p class="empty-state__hint">「＋ 新規追加」から登録できます。</p>
      </div>
    `;
  }

  const totalMonthly = subscriptions
    .filter(s => s.isActive)
    .reduce((sum, s) => sum + monthlyEquivalent(s), 0);

  const activeCount = subscriptions.filter(s => s.isActive).length;
  const items = subscriptions.map(buildSubscriptionCard).join('');

  return `
    <div class="subscription-summary">
      <div class="subscription-summary__card">
        <div class="subscription-summary__label">月額合計（有効なもの）</div>
        <div class="subscription-summary__value">${escapeHtml(formatCurrency(Math.round(totalMonthly)))}</div>
        <div class="subscription-summary__sub">${activeCount} 件が有効 / 全 ${subscriptions.length} 件</div>
      </div>
    </div>
    <ul class="sub-list" aria-label="サブスクリプション一覧">${items}</ul>
  `;
}

/**
 * 請求サイクルを月額換算する。
 * 週次の 4.33 は 52 週 / 12 か月（＝平均月あたりの週数）。
 */
function monthlyEquivalent(sub) {
  if (sub.billingCycle === 'yearly') return sub.amount / 12;
  if (sub.billingCycle === 'weekly') return sub.amount * 4.33;
  return sub.amount;
}

/**
 * 次回支払日までの日数。時刻成分を落として「日」単位で比較する
 * （そうしないと同じ日でも実行時刻によって 0 日/1 日が揺れる）。
 */
function daysUntil(dateValue) {
  const target = new Date(dateValue);
  if (isNaN(target.getTime())) return null;

  const startOfToday = new Date();
  startOfToday.setHours(0, 0, 0, 0);
  target.setHours(0, 0, 0, 0);

  return Math.round((target - startOfToday) / (1000 * 60 * 60 * 24));
}

function buildSubscriptionCard(sub) {
  const cycleLabel = CYCLE_LABELS[sub.billingCycle] ?? sub.billingCycle;
  const remaining = daysUntil(sub.nextBillingDate);
  const isDueSoon = sub.isActive && remaining !== null && remaining >= 0 && remaining <= DUE_SOON_DAYS;
  const name = escapeHtml(sub.serviceName);

  const dueBadge = isDueSoon
    ? `<span class="badge badge--warning">${remaining === 0 ? '今日' : `あと${remaining}日`}</span>`
    : '';
  const inactiveBadge = sub.isActive
    ? ''
    : `<span class="badge badge--muted">無効</span>`;

  return `
    <li class="sub-card${isDueSoon ? ' sub-card--due' : ''}${sub.isActive ? '' : ' sub-card--inactive'}" data-id="${sub.id}">
      <div class="sub-card__body">
        <div class="sub-card__headline">
          <span class="sub-card__name">${name}</span>
          ${dueBadge}${inactiveBadge}
        </div>
        <div class="sub-card__meta">
          <span class="sub-card__cycle">${escapeHtml(cycleLabel)}</span>
          <span class="sub-card__dot" aria-hidden="true">・</span>
          <span class="sub-card__date">次回 ${escapeHtml(formatDate(sub.nextBillingDate))}</span>
        </div>
      </div>
      <div class="sub-card__amount">${escapeHtml(formatCurrency(sub.amount))}</div>
      <div class="sub-card__actions">
        <button type="button"
                class="btn btn--ghost btn--sm"
                data-action="edit"
                data-id="${sub.id}"
                aria-label="${name}を編集">編集</button>
        <button type="button"
                class="btn btn--ghost btn--sm btn--danger-text"
                data-action="delete"
                data-id="${sub.id}"
                aria-label="${name}を削除">削除</button>
      </div>
    </li>
  `;
}

// ---------------------------------------------------------------------------
// Event listeners — shell 側に 1 回だけ貼る
// ---------------------------------------------------------------------------

/**
 * shell（＝再描画されない DOM）へのリスナー登録。
 * **renderSubscriptionPage() からのみ、1 回だけ呼ぶこと。**
 *
 * @param {HTMLElement} container
 * @param {{subscriptions: Array, busy: boolean, escHandler: ?Function}} state
 */
function attachShellListeners(container, state) {
  // 新規追加（ヘッダーのボタンと、モバイル用の FAB は同じ動作）
  container.querySelector('#add-subscription-btn')?.addEventListener('click', () => {
    openModal(container, null);
  });
  container.querySelector('#add-subscription-fab')?.addEventListener('click', () => {
    openModal(container, null);
  });

  // 一覧の編集/削除はイベント委譲で扱う。委譲元 #subscription-content は
  // 永続要素なので、中身が何度差し替わってもこのリスナーは 1 本のまま。
  container.querySelector('#subscription-content')?.addEventListener('click', (e) => {
    const actionBtn = e.target.closest('[data-action]');
    if (!actionBtn) return;

    const id = Number(actionBtn.getAttribute('data-id'));
    const sub = state.subscriptions.find(s => s.id === id);
    if (!sub) return;

    if (actionBtn.getAttribute('data-action') === 'edit') {
      openModal(container, sub);
    } else if (actionBtn.getAttribute('data-action') === 'delete') {
      deleteSubscription(container, state, sub);
    }
  });

  // モーダルを閉じる
  container.querySelector('#modal-close-btn')?.addEventListener('click', () => closeModal(container));
  container.querySelector('#modal-cancel-btn')?.addEventListener('click', () => closeModal(container));
  container.querySelector('#modal-backdrop')?.addEventListener('click', () => closeModal(container));

  // ESC キー。document に貼るので teardown() で必ず外す。
  state.escHandler = (e) => {
    if (e.key === 'Escape') closeModal(container);
  };
  document.addEventListener('keydown', state.escHandler);

  // 保存
  container.querySelector('#modal-save-btn')?.addEventListener('click', () => {
    handleSave(container, state);
  });

  // モーダル内の削除（編集中のものを削除する）
  container.querySelector('#modal-delete-btn')?.addEventListener('click', () => {
    const id = Number(container.querySelector('#subscription-id').value);
    const sub = state.subscriptions.find(s => s.id === id);
    if (sub) deleteSubscription(container, state, sub);
  });

  // フォームの暗黙送信（入力欄で Enter）でも保存できるようにする。
  // 保存ボタンは type="button" なので、これが無いと Enter が何もしない。
  container.querySelector('#subscription-form')?.addEventListener('submit', (e) => {
    e.preventDefault();
    handleSave(container, state);
  });
}

// ---------------------------------------------------------------------------
// Modal
// ---------------------------------------------------------------------------

function openModal(container, subscription) {
  const modal = container.querySelector('#subscription-modal');
  const title = container.querySelector('#modal-title');
  const deleteBtn = container.querySelector('#modal-delete-btn');
  const idInput = container.querySelector('#subscription-id');
  if (!modal) return;

  if (subscription) {
    title.textContent = 'サブスクリプションを編集';
    deleteBtn.hidden = false;
    idInput.value = subscription.id;

    container.querySelector('#service-name').value = subscription.serviceName ?? '';
    container.querySelector('#amount').value = subscription.amount ?? '';
    container.querySelector('#billing-cycle').value = subscription.billingCycle ?? 'monthly';
    container.querySelector('#next-billing-date').value = toDateInputValue(subscription.nextBillingDate);
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
  document.body.classList.add('body--modal-open');

  container.querySelector('#service-name')?.focus();
}

function closeModal(container) {
  const modal = container.querySelector('#subscription-modal');
  if (!modal || modal.hidden) return;

  modal.hidden = true;
  document.body.classList.remove('body--modal-open');
  container.querySelector('#add-subscription-btn')?.focus();
}

/**
 * `<input type="date">` は "YYYY-MM-DD" しか受け付けない。
 * サーバーが ISO 8601（"2026-08-01T00:00:00"）を返しても空欄にならないよう、
 * 日付部分だけを切り出す。
 */
function toDateInputValue(value) {
  if (!value) return '';
  const text = String(value);
  const match = text.match(/^\d{4}-\d{2}-\d{2}/);
  if (match) return match[0];

  const parsed = new Date(text);
  if (isNaN(parsed.getTime())) return '';
  const y = parsed.getFullYear();
  const m = String(parsed.getMonth() + 1).padStart(2, '0');
  const d = String(parsed.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

function clearForm(container) {
  const today = new Date();
  const nextMonth = new Date(today.getFullYear(), today.getMonth() + 1, today.getDate());

  container.querySelector('#service-name').value = '';
  container.querySelector('#amount').value = '';
  container.querySelector('#billing-cycle').value = 'monthly';
  container.querySelector('#next-billing-date').value = toDateInputValue(nextMonth);
  container.querySelector('#notes').value = '';
  container.querySelector('#is-active').checked = true;
}

// ---------------------------------------------------------------------------
// Save / Delete
// ---------------------------------------------------------------------------

async function handleSave(container, state) {
  // 多重送信ガード。リスナーが万一重複しても、通信中の再タップでも、
  // ここで 2 発目以降を落とせる。二重登録に対する最後の砦。
  if (state.busy) return;

  clearValidationErrors(container);

  const id = container.querySelector('#subscription-id').value;
  const serviceName = container.querySelector('#service-name').value.trim();
  const amount = container.querySelector('#amount').value;
  const billingCycle = container.querySelector('#billing-cycle').value;
  const nextBillingDate = container.querySelector('#next-billing-date').value;
  const notes = container.querySelector('#notes').value.trim();
  const isActive = container.querySelector('#is-active').checked;

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
  state.busy = true;
  saveBtn.disabled = true;
  saveBtn.textContent = '保存中...';

  try {
    const payload = { serviceName, amount: amountNum, billingCycle, nextBillingDate, notes, isActive };

    if (id) {
      await api.put(`/subscriptions/${id}`, payload);
      toast.show('サブスクリプションを更新しました', 'success');
    } else {
      await api.post('/subscriptions', payload);
      toast.show('サブスクリプションを登録しました', 'success');
    }

    closeModal(container);
    await loadAndRender(container, state);
  } catch (err) {
    // 409 はサーバー側の重複防御。通信タイムアウト後に再送した場合など、
    // フロントのガードをすり抜けた重複はここで止まる。
    const message = err?.status === 409
      ? (err.message || '同じ名前のサブスクリプションが既に登録されています。')
      : (err?.message ?? '保存に失敗しました。');
    showFieldError(container, 'service-name-error', message);
  } finally {
    state.busy = false;
    saveBtn.disabled = false;
    saveBtn.textContent = '保存';
  }
}

/**
 * 削除の確認から実行まで。一覧の削除ボタンとモーダルの削除ボタンで共用する。
 *
 * 確認 UI には共通コンポーネント（ff-confirm-dialog）を使う。
 * 以前はモーダルフッターの innerHTML を文字列で退避 → 復元していたが、
 * 復元された要素は**リスナーを 1 本も持たない別物**になるため、
 * 「削除 → キャンセル」を一度でも操作すると保存・削除が無反応になっていた。
 */
async function deleteSubscription(container, state, sub) {
  if (state.busy) return;

  const confirmed = await confirmDialog.show({
    title: 'サブスクリプションの削除',
    message: `「${sub.serviceName}」を削除しますか？この操作は取り消せません。`,
    confirmLabel: '削除する',
    cancelLabel: 'キャンセル',
    danger: true,
  });

  if (!confirmed) return;

  state.busy = true;
  try {
    await api.delete(`/subscriptions/${sub.id}`);
    toast.show('サブスクリプションを削除しました', 'success');
    closeModal(container);
    await loadAndRender(container, state);
  } catch (err) {
    toast.show(err?.message ?? '削除に失敗しました。', 'error');
  } finally {
    state.busy = false;
  }
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
