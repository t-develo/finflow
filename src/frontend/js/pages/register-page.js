/**
 * register-page.js - User registration screen
 *
 * Route: /register
 * API:   POST /api/auth/register  (mock in Sprint 1)
 *
 * On success: navigates to /login with a success message.
 * Validates:  email format, password >= 8 chars, passwords match.
 */

import { auth } from '../utils/auth.js';
import { api } from '../utils/api-client.js';
import { router } from '../router.js';
import { toast } from '../components/ff-toast.js';

const authApi = {
  register: (payload) => api.post('/auth/register', payload, { showLoader: true }),
};

// ---------------------------------------------------------------------------
// Validation
// ---------------------------------------------------------------------------

function validateField(name, value, allValues) {
  switch (name) {
    case 'name':
      if (!value.trim()) return 'ユーザー名を入力してください';
      return '';

    case 'email':
      if (!value.trim()) return 'メールアドレスを入力してください';
      if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value)) return '有効なメールアドレスを入力してください';
      return '';

    case 'password':
      if (!value) return 'パスワードを入力してください';
      if (value.length < 8) return 'パスワードは8文字以上で入力してください';
      return '';

    case 'confirmPassword':
      if (!value) return 'パスワード（確認）を入力してください';
      if (value !== allValues.password) return 'パスワードが一致しません';
      return '';

    default:
      return '';
  }
}

function validateAll(values) {
  const errors = {};
  for (const [name, value] of Object.entries(values)) {
    const msg = validateField(name, value, values);
    if (msg) errors[name] = msg;
  }
  return errors;
}

// ---------------------------------------------------------------------------
// Render
// ---------------------------------------------------------------------------

/**
 * @param {HTMLElement} container
 */
export function renderRegisterPage(container) {
  container.innerHTML = buildHtml();
  attachEventListeners(container);
}

function buildHtml() {
  return `
    <div class="auth-layout">
      <div class="auth-card">
        <div class="auth-card__logo" aria-label="FinFlow">
          <span aria-hidden="true">💰</span>
          <span>FinFlow</span>
        </div>

        <h1 class="auth-card__title">新規登録</h1>

        <div id="register-error" class="alert alert--error" hidden role="alert"></div>

        <form id="register-form" novalidate>
          <div class="form-group">
            <label class="form-group__label form-group__label--required" for="reg-name">
              ユーザー名
            </label>
            <input
              class="form-group__input"
              type="text"
              id="reg-name"
              name="name"
              autocomplete="name"
              placeholder="山田 太郎"
              required
              aria-required="true"
              aria-describedby="reg-name-error"
            >
            <span class="form-group__error" id="reg-name-error" role="alert"></span>
          </div>

          <div class="form-group">
            <label class="form-group__label form-group__label--required" for="reg-email">
              メールアドレス
            </label>
            <input
              class="form-group__input"
              type="email"
              id="reg-email"
              name="email"
              autocomplete="email"
              placeholder="you@example.com"
              required
              aria-required="true"
              aria-describedby="reg-email-error"
            >
            <span class="form-group__error" id="reg-email-error" role="alert"></span>
          </div>

          <div class="form-group">
            <label class="form-group__label form-group__label--required" for="reg-password">
              パスワード
            </label>
            <input
              class="form-group__input"
              type="password"
              id="reg-password"
              name="password"
              autocomplete="new-password"
              placeholder="8文字以上"
              required
              aria-required="true"
              aria-describedby="reg-password-error"
            >
            <span class="form-group__error" id="reg-password-error" role="alert"></span>
          </div>

          <div class="form-group">
            <label class="form-group__label form-group__label--required" for="reg-confirm-password">
              パスワード（確認）
            </label>
            <input
              class="form-group__input"
              type="password"
              id="reg-confirm-password"
              name="confirmPassword"
              autocomplete="new-password"
              placeholder="もう一度入力してください"
              required
              aria-required="true"
              aria-describedby="reg-confirmPassword-error"
            >
            <span class="form-group__error" id="reg-confirmPassword-error" role="alert"></span>
          </div>

          <button class="btn btn--primary btn--lg" type="submit" id="reg-submit-btn">
            登録する
          </button>
        </form>

        <div class="auth-card__footer">
          すでにアカウントをお持ちの方は
          <a href="/login" data-navigo>ログイン</a>
        </div>
      </div>
    </div>
  `;
}

function attachEventListeners(container) {
  const form = container.querySelector('#register-form');

  // Blur validation for each field
  const fieldIds = ['name', 'email', 'password', 'confirmPassword'];
  const inputIds = { name: 'reg-name', email: 'reg-email', password: 'reg-password', confirmPassword: 'reg-confirm-password' };

  fieldIds.forEach(fieldName => {
    const inputEl = container.querySelector(`#${inputIds[fieldName]}`);
    inputEl?.addEventListener('blur', () => {
      const values = collectValues(container);
      const msg = validateField(fieldName, values[fieldName], values);
      showFieldError(container, fieldName, msg);
    });
  });

  form.addEventListener('submit', async (e) => {
    e.preventDefault();
    await handleSubmit(container);
  });
}

function collectValues(container) {
  return {
    name: container.querySelector('#reg-name')?.value.trim() || '',
    email: container.querySelector('#reg-email')?.value.trim() || '',
    password: container.querySelector('#reg-password')?.value || '',
    confirmPassword: container.querySelector('#reg-confirm-password')?.value || '',
  };
}

async function handleSubmit(container) {
  const values = collectValues(container);
  const errors = validateAll(values);

  // Show all field errors
  Object.keys(errors).forEach(field => showFieldError(container, field, errors[field]));
  // Clear fields with no error
  ['name', 'email', 'password', 'confirmPassword'].forEach(field => {
    if (!errors[field]) showFieldError(container, field, '');
  });

  if (Object.keys(errors).length > 0) return;

  const submitBtn = container.querySelector('#reg-submit-btn');
  setSubmitting(submitBtn, true);
  hideGlobalError(container);

  try {
    const result = await authApi.register(values);
    auth.setToken(result.token, result.user);
    toast.show('登録が完了しました！', 'success');
    router.navigate('/');
  } catch (err) {
    showGlobalError(container, err.message || '登録に失敗しました。もう一度お試しください。');
  } finally {
    setSubmitting(submitBtn, false);
  }
}

// ---------------------------------------------------------------------------
// UI helpers
// ---------------------------------------------------------------------------

function showFieldError(container, fieldName, message) {
  const errorEl = container.querySelector(`#reg-${fieldName}-error`);
  // Map fieldName to input id
  const inputIdMap = {
    name: 'reg-name',
    email: 'reg-email',
    password: 'reg-password',
    confirmPassword: 'reg-confirm-password',
  };
  const inputEl = container.querySelector(`#${inputIdMap[fieldName]}`);

  if (errorEl) errorEl.textContent = message;
  if (inputEl) {
    inputEl.classList.toggle('form-group__input--error', Boolean(message));
  }
}

function showGlobalError(container, message) {
  const el = container.querySelector('#register-error');
  if (!el) return;
  el.textContent = message;
  el.removeAttribute('hidden');
}

function hideGlobalError(container) {
  container.querySelector('#register-error')?.setAttribute('hidden', '');
}

function setSubmitting(btn, isSubmitting) {
  if (!btn) return;
  btn.disabled = isSubmitting;
  btn.textContent = isSubmitting ? '登録中...' : '登録する';
}
