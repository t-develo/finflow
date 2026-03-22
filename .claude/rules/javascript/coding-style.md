# JavaScript Coding Style — FinFlow Frontend

## ファイル・命名規則

```
ファイル名:  kebab-case.js  (例: expense-form.js, api-client.js)
クラス名:    PascalCase     (例: ExpenseForm, ApiClient)
変数・関数:  camelCase      (例: getExpenses, totalAmount)
定数:        UPPER_SNAKE    (例: API_BASE_URL, MAX_ROWS)
Web Components: ff- プレフィックス (例: ff-expense-form, ff-dashboard)
CSS クラス:  BEM記法        (例: .expense-form__input--error)
```

## ES2020+ 機能を活用

```javascript
// Optional chaining
const categoryName = expense?.category?.name ?? '未分類';

// Nullish coalescing
const amount = userInput ?? 0;

// Destructuring
const { id, amount, description } = expense;

// async/await（Promise チェーンより優先）
const expenses = await apiClient.get('/expenses');

// Private class fields
class ExpenseForm extends HTMLElement {
    #currentExpense = null;
}
```

## Web Components パターン

```javascript
class FfExpenseForm extends HTMLElement {
    // ライフサイクルメソッド
    connectedCallback() {
        this.render();
        this.#attachEventListeners();
    }

    disconnectedCallback() {
        // クリーンアップ
    }

    // プライベートメソッド
    #attachEventListeners() { }

    render() {
        this.innerHTML = `
            <form class="expense-form">
                <input class="expense-form__input" type="number" name="amount">
            </form>
        `;
    }
}

customElements.define('ff-expense-form', FfExpenseForm);
```

## エラーハンドリング

```javascript
// NG: エラーを握りつぶす
try {
    await apiClient.post('/expenses', data);
} catch (e) {}

// OK: エラーを適切に処理・表示
try {
    await apiClient.post('/expenses', data);
    this.#showSuccess('支出を登録しました');
} catch (error) {
    this.#showError(error.message ?? '登録に失敗しました');
    console.error('[ExpenseForm]', error);
}
```

## モジュール構成

ビルドステップなし、ES Modules (`import`/`export`) を使用:

```javascript
// api-client.js からインポート
import { apiClient } from '../utils/api-client.js';
import { auth } from '../utils/auth.js';
```
