# FinFlow フロントエンドSE実装ガイド【SE-3】

バニラJS + Web ComponentsによるSPA開発の規約と注意点。

---

## コーディング規約

### JavaScript命名規則

```javascript
// ファイル名: kebab-case
expense-form.js, api-client.js, auth.js

// クラス名: PascalCase
class FfExpenseForm extends HTMLElement {}
class ExpensePage {}

// メソッド・変数: camelCase
handleSubmit(), isLoading, amountInput

// 定数: UPPER_SNAKE_CASE
const MAX_FILE_SIZE = 10 * 1024 * 1024;
const API_BASE_URL = '/api';

// Web Componentsタグ名: ff-プレフィックス
<ff-expense-form>, <ff-expense-list>, <ff-dashboard-card>

// イベントハンドラ: handle + イベント名
handleSubmit(), handleFilterChange(), handleDeleteClick()

// 状態フラグ: is/has/can プレフィックス
isLoading, isAuthenticated, hasError, canSubmit
```

### CSS規約（BEM記法）

```css
/* BEM: .block__element--modifier */
.expense-form {}
.expense-form__input {}
.expense-form__input--error {}
.expense-form__button--loading {}

/* CSS変数を必ず使う（ハードコード禁止） */
/* BAD */
color: #3498db;
margin: 8px;

/* GOOD */
color: var(--color-primary);
margin: var(--spacing-sm);
```

---

## Web Componentsの基本パターン

```javascript
class FfExpenseForm extends HTMLElement {
    constructor() {
        super();
        this.attachShadow({ mode: 'open' });
    }

    // DOM接続時: レンダリング + イベント登録
    connectedCallback() {
        this.render();
        this.setupEventListeners();
    }

    // DOM切断時: イベント解除（メモリリーク防止）
    disconnectedCallback() {
        this.cleanup();
    }

    // 監視する属性を宣言
    static get observedAttributes() {
        return ['expense-id', 'mode'];
    }

    // 属性変更時の処理
    attributeChangedCallback(name, oldValue, newValue) {
        if (oldValue !== newValue) {
            this.render();
        }
    }
}

customElements.define('ff-expense-form', FfExpenseForm);
```

**重要:** イベントリスナーを `disconnectedCallback` で必ず解除する。解除しないとメモリリークになる。

---

## セキュリティ（XSS防止）

```javascript
// BAD: XSS脆弱性（ユーザー入力を直接innerHTML）
this.shadowRoot.innerHTML = `<span>${userInput}</span>`;

// GOOD: 必ずエスケープしてから埋め込む
this.shadowRoot.innerHTML = `<span>${this.escapeHtml(userInput)}</span>`;

// エスケープ関数（全コンポーネントで共通使用）
escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

// その他の禁止事項
eval(userInput);          // 絶対禁止
window.userScript;        // グローバル変数禁止
console.log(data);        // プロダクションコードに残さない
alert('エラー!');          // カスタムUIを使う（alert/confirm/prompt禁止）
```

---

## API通信の統一ルール

```javascript
// 全APIリクエストは api-client.js を経由する（直接fetchしない）
// BAD
const response = await fetch('/api/expenses', { headers: { ... } });

// GOOD
import { apiClient } from '../utils/api-client.js';
const expenses = await apiClient.get('/expenses');

// Sprint 1: モックAPIを使用
// Sprint 2: api-client.jsのベースURLを変更するだけで実APIに切り替わる
// → 各ページ・コンポーネントのコードは変更不要
```

---

## UX設計の必須対応

### フォームのユーザビリティ

```javascript
// ローディング状態（即座に表示）
async handleSubmit(e) {
    e.preventDefault();
    this.setLoading(true);
    try {
        await apiClient.post('/expenses', this.getFormData());
        this.showSuccess('支出を追加しました');
    } catch (err) {
        this.showError(err.message); // 「エラーが発生しました」は不可
    } finally {
        this.setLoading(false);
    }
}

// 二重送信防止
setLoading(isLoading) {
    this.submitButton.disabled = isLoading;
    this.submitButton.textContent = isLoading ? '送信中...' : '登録する';
}

// バリデーション（blur時にリアルタイム検証）
amountInput.addEventListener('blur', () => this.validateAmount());

// デフォルト値（日付フィールドは今日の日付）
dateInput.value = new Date().toISOString().split('T')[0];
```

---

## 状態管理のルール

```javascript
// JWT認証はauth.jsで一元管理
// 他モジュールはlocalStorageに直接アクセスしない
import { auth } from '../utils/auth.js';
const token = auth.getToken();

// 401レスポンス時は自動リダイレクト（api-client.jsで処理）
// ページコンポーネントでの個別処理は不要

// フィルタ条件はURLクエリパラメータに反映
const params = new URLSearchParams({ month: '2026-03', category: '食費' });
history.pushState({}, '', `?${params}`);

// ページ遷移時にリスナー・タイマーをクリーンアップ
disconnectedCallback() {
    clearInterval(this._pollTimer);
    this._abortController?.abort();
}
```

---

## パフォーマンス

```javascript
// 検索・フィルタ入力にデバウンス（300ms）
let debounceTimer;
searchInput.addEventListener('input', (e) => {
    clearTimeout(debounceTimer);
    debounceTimer = setTimeout(() => this.filterExpenses(e.target.value), 300);
});

// イベントデリゲーション（リスト内の個別リスナーは避ける）
// BAD: 各行にリスナー
items.forEach(item => item.addEventListener('click', handleClick));
// GOOD: 親要素で委譲
listContainer.addEventListener('click', (e) => {
    if (e.target.closest('[data-action="delete"]')) handleDelete(e);
});
```

---

## 禁止事項

- `innerHTML` にユーザー入力をエスケープせず埋め込まない（XSS）
- `eval()` を使わない
- グローバル変数を使わない（モジュールスコープで管理）
- イベントリスナーを `disconnectedCallback` で解除しない（メモリリーク）
- CSSにハードコードされた色・サイズを書かない（CSS変数を使う）
- `alert()` / `confirm()` / `prompt()` を使わない
- ページやコンポーネントから直接 `fetch()` を呼ばない（api-client.js経由）
- PLに相談せずにUI仕様を変更しない
- `console.log` をプロダクションコードに残さない
- `var` を使わない（`const` 優先、変更が必要な場合のみ `let`）
