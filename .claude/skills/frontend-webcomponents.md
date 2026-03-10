# フロントエンド実装ガイド（バニラJS + Web Components）【汎用】

フレームワークなし・バニラJS + Web Componentsで構築するSPAの実装規約。

---

## JavaScript命名規則

```javascript
// ファイル名: kebab-case
user-form.js, api-client.js, auth.js

// クラス名: PascalCase
class UserForm extends HTMLElement {}
class DashboardPage {}

// メソッド・変数: camelCase
handleSubmit(), isLoading, nameInput

// 定数: UPPER_SNAKE_CASE
const MAX_FILE_SIZE = 10 * 1024 * 1024;
const API_BASE_URL = '/api';

// イベントハンドラ: handle + イベント名
handleSubmit(), handleFilterChange(), handleDeleteClick()

// 状態フラグ: is/has/can プレフィックス
isLoading, isAuthenticated, hasError, canSubmit

// var禁止（constを優先、変更が必要な場合のみlet）
const items = [];    // OK
let count = 0;       // OK（変更あり）
var name = '';       // NG
```

---

## Web Componentsの基本パターン

```javascript
class MyComponent extends HTMLElement {
    constructor() {
        super();
        this.attachShadow({ mode: 'open' });
    }

    // DOM接続時: レンダリング + イベント登録
    connectedCallback() {
        this.render();
        this.setupEventListeners();
    }

    // DOM切断時: 必ずイベント解除（メモリリーク防止）
    disconnectedCallback() {
        this.cleanup();
    }

    // 監視する属性を宣言
    static get observedAttributes() {
        return ['item-id', 'mode'];
    }

    // 属性変更時の処理
    attributeChangedCallback(name, oldValue, newValue) {
        if (oldValue !== newValue) {
            this.render();
        }
    }
}

customElements.define('my-component', MyComponent);
```

**重要:** イベントリスナーを `disconnectedCallback` で必ず解除する。解除しないとメモリリークになる。

---

## CSS規約（BEM記法）

```css
/* BEM: .block__element--modifier */
.user-form {}
.user-form__input {}
.user-form__input--error {}
.user-form__button--loading {}

/* CSS変数を必ず使う（ハードコード禁止） */
/* BAD */
color: #3498db;
margin: 8px;

/* GOOD */
color: var(--color-primary);
margin: var(--spacing-sm);
```

---

## セキュリティ（XSS防止）

```javascript
// BAD: XSS脆弱性（ユーザー入力を直接innerHTML）
this.shadowRoot.innerHTML = `<span>${userInput}</span>`;

// GOOD: 必ずエスケープしてから埋め込む
this.shadowRoot.innerHTML = `<span>${this.escapeHtml(userInput)}</span>`;

// エスケープ関数
escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}
```

### 禁止事項（絶対NG）

```javascript
eval(userInput);         // 絶対禁止
alert('エラー!');         // alert/confirm/prompt禁止（カスタムUIを使う）
console.log(data);       // プロダクションコードに残さない
window.globalVar = ...;  // グローバル変数禁止（モジュールスコープで管理）
var x = ...;             // var禁止
```

---

## API通信の統一ルール

```javascript
// 全APIリクエストはAPIクライアントを経由する（直接fetchしない）
// BAD
const response = await fetch('/api/items', { headers: { 'Authorization': `Bearer ${token}` } });

// GOOD: APIクライアント経由（JWT自動付与）
import { apiClient } from '../utils/api-client.js';
const items = await apiClient.get('/items');
```

---

## UX設計の必須対応

### フォーム

```javascript
// ローディング状態（クリック後即座に表示）
async handleSubmit(e) {
    e.preventDefault();
    this.setLoading(true);
    try {
        await apiClient.post('/items', this.getFormData());
        this.showSuccess('登録しました');
    } catch (err) {
        this.showError(err.message); // 「エラーが発生しました」という曖昧な表示はNG
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
input.addEventListener('blur', () => this.validate());
```

---

## パフォーマンス

```javascript
// 検索・フィルタ入力にデバウンス（300ms目安）
let debounceTimer;
searchInput.addEventListener('input', (e) => {
    clearTimeout(debounceTimer);
    debounceTimer = setTimeout(() => this.filter(e.target.value), 300);
});

// イベントデリゲーション（リスト内の個別リスナーは避ける）
// BAD: 各行にリスナー
items.forEach(item => item.addEventListener('click', handleClick)); // N個のリスナー
// GOOD: 親要素で委譲
listContainer.addEventListener('click', (e) => {
    if (e.target.closest('[data-action="delete"]')) handleDelete(e);
});
```

---

## 依存関係の方向性

```
[Pages] → [Web Components] → [Utils / api-client.js]
  具体的         汎用的               インフラ層
```

- ページは複数のコンポーネントを組み合わせる
- コンポーネントはユーティリティを使う
- ユーティリティ・APIクライアントは他に依存しない
