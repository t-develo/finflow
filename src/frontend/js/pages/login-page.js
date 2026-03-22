/**
 * login-page.js - Login screen
 *
 * Route: /login
 * API:   POST /api/auth/login  (mock in Sprint 1)
 *
 * On success: saves JWT to localStorage and navigates to /.
 * On failure: shows an inline error message.
 */

import { auth } from '../utils/auth.js';
import { api } from '../utils/api-client.js';
import { router } from '../router.js';
import { toast } from '../components/ff-toast.js';

const authApi = {
  login: (payload) => api.post('/auth/login', payload, { showLoader: true }),
};

/** Validation rules */
function validateLoginForm({ email, password }) {
  const errors = {};
  if (!email) {
    errors.email = 'メールアドレスを入力してください';
  } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
    errors.email = '有効なメールアドレスを入力してください';
  }
  if (!password) {
    errors.password = 'パスワードを入力してください';
  } else if (password.length < 8) {
    errors.password = 'パスワードは8文字以上で入力してください';
  }
  return errors;
}

/**
 * Render the login page into the given container element.
 * @param {HTMLElement} container
 */
export function renderLoginPage(container) {
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

        <h1 class="auth-card__title">ログイン</h1>

        <div id="login-error" class="alert alert--error" hidden role="alert"></div>

        <form id="login-form" novalidate>
          <div class="form-group">
            <label class="form-group__label form-group__label--required" for="login-email">
              メールアドレス
            </label>
            <input
              class="form-group__input"
              type="email"
              id="login-email"
              name="email"
              autocomplete="email"
              placeholder="you@example.com"
              required
              aria-required="true"
              aria-describedby="login-email-error"
            >
            <span class="form-group__error" id="login-email-error" role="alert"></span>
          </div>

          <div class="form-group">
            <label class="form-group__label form-group__label--required" for="login-password">
              パスワード
            </label>
            <input
              class="form-group__input"
              type="password"
              id="login-password"
              name="password"
              autocomplete="current-password"
              placeholder="パスワードを入力"
              required
              aria-required="true"
              aria-describedby="login-password-error"
            >
            <span class="form-group__error" id="login-password-error" role="alert"></span>
          </div>

          <button class="btn btn--primary btn--lg" type="submit" id="login-submit-btn">
            ログイン
          </button>
        </form>

        <div class="auth-card__footer">
          アカウントをお持ちでない方は
          <a href="/register" data-navigo>新規登録</a>
        </div>
      </div>
    </div>
  `;
}

function attachEventListeners(container) {
  const form = container.querySelector('#login-form');
  const submitBtn = container.querySelector('#login-submit-btn');

  // Inline validation on blur
  const emailInput = container.querySelector('#login-email');
  const passwordInput = container.querySelector('#login-password');

  emailInput.addEventListener('blur', () => {
    const errors = validateLoginForm({ email: emailInput.value, password: 'placeholder' });
    showFieldError(container, 'email', errors.email || '');
  });

  passwordInput.addEventListener('blur', () => {
    const errors = validateLoginForm({ email: emailInput.value, password: passwordInput.value });
    showFieldError(container, 'password', errors.password || '');
  });

  form.addEventListener('submit', async (e) => {
    e.preventDefault();
    await handleSubmit(container, submitBtn);
  });
}

async function handleSubmit(container, submitBtn) {
  const email = container.querySelector('#login-email').value.trim();
  const password = container.querySelector('#login-password').value;

  // Validate
  const errors = validateLoginForm({ email, password });
  showFieldError(container, 'email', errors.email || '');
  showFieldError(container, 'password', errors.password || '');
  if (Object.keys(errors).length > 0) return;

  // Submit
  setSubmitting(submitBtn, true);
  hideGlobalError(container);

  try {
    const result = await authApi.login({ email, password });
    auth.setToken(result.token, result.user);
    router.navigate('/');
  } catch (err) {
    toast.show(
      err?.message || 'メールアドレスまたはパスワードが正しくありません',
      'error'
    );
    showGlobalError(container, err?.message || 'メールアドレスまたはパスワードが正しくありません');
  } finally {
    setSubmitting(submitBtn, false);
  }
}

// ---------------------------------------------------------------------------
// UI helpers
// ---------------------------------------------------------------------------

function showFieldError(container, fieldName, message) {
  const errorEl = container.querySelector(`#login-${fieldName}-error`);
  const inputEl = container.querySelector(`#login-${fieldName}`);
  if (!errorEl || !inputEl) return;

  errorEl.textContent = message;
  if (message) {
    inputEl.classList.add('form-group__input--error');
  } else {
    inputEl.classList.remove('form-group__input--error');
  }
}

function showGlobalError(container, message) {
  const el = container.querySelector('#login-error');
  if (!el) return;
  el.textContent = message;
  el.removeAttribute('hidden');
}

function hideGlobalError(container) {
  const el = container.querySelector('#login-error');
  el?.setAttribute('hidden', '');
}

function setSubmitting(btn, isSubmitting) {
  btn.disabled = isSubmitting;
  btn.textContent = isSubmitting ? '確認中...' : 'ログイン';
}
