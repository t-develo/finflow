# JavaScript Testing — FinFlow Frontend

## テスト戦略

ビルドステップがないため、フロントエンドのテストは以下を優先:
1. **API クライアントのユニットテスト** — リクエスト/レスポンスのハンドリング
2. **ユーティリティ関数** — auth.js, date formatting 等
3. **Web Components の動作確認** — ブラウザでの手動確認 + スナップショット

## モックの活用（Sprint 1）

Sprint 1 では実APIの代わりにモックを使用:

```javascript
// js/mocks/expenses.mock.js
export const mockExpenses = [
    { id: 1, amount: 1500, description: '昼食', date: '2026-03-22', categoryId: 1 },
    { id: 2, amount: 200, description: 'コーヒー', date: '2026-03-22', categoryId: 1 },
];

// モック切り替えフラグ
export const USE_MOCK = true;
```

## APIクライアントのテスト観点

```javascript
// api-client.js のテスト確認事項
// 1. JWT トークンが Authorization ヘッダーに自動付与される
// 2. 401レスポンスでログイン画面にリダイレクトされる
// 3. ネットワークエラーが適切に処理される
```

## フォームバリデーション確認項目

- 必須フィールドの空欄チェック
- 金額の正数チェック（0以下は無効）
- 日付フォーマットの検証
- エラーメッセージの表示確認（`.expense-form__input--error` クラス）

## Sprint 2 への移行

```javascript
// Sprint 1: モックを使用
import { mockExpenses } from '../mocks/expenses.mock.js';

// Sprint 2: 実APIに切り替え
import { apiClient } from '../utils/api-client.js';
const expenses = await apiClient.get('/api/expenses');
```
