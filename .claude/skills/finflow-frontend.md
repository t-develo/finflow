# FinFlow フロントエンド固有ガイド【FinFlow固有】

SE-3が意識すべきFinFlow固有のフロントエンド実装ルール。
バニラJS + Web Componentsの汎用的な実装規約は `/frontend-webcomponents` を参照。
XSSなどのセキュリティ要件は `/finflow-security` を参照。

---

## FinFlow固有の命名規則

### Web Components タグ名

必ず `ff-` プレフィックスを使用する。

```javascript
// FinFlow固有のプレフィックス
<ff-expense-form>
<ff-expense-list>
<ff-subscription-card>
<ff-dashboard-summary>
<ff-category-badge>
```

---

## API通信アーキテクチャ

### api-client.js がバックエンドとの唯一の境界

```javascript
// src/frontend/js/utils/api-client.js

// Sprint 1: モックAPIを使用（バックエンド完成前に開発）
// Sprint 2: このファイルのベースURLを変更するだけで実APIに切り替わる
// → ページやコンポーネントのコードは変更不要

// BAD: ページから直接fetch
const response = await fetch('/api/expenses', {
    headers: { 'Authorization': `Bearer ${localStorage.getItem('token')}` }
});

// GOOD: api-client.js経由（JWTの付与は自動）
import { apiClient } from '../utils/api-client.js';
const expenses = await apiClient.get('/expenses');
```

### Sprint 1 → Sprint 2 の移行ルール

- Sprint 1: `js/mocks/` のモックAPIを `api-client.js` が参照する
- Sprint 2: `api-client.js` のベースURL切り替えのみ（PR 1枚で切り替え完了）
- 各ページ・コンポーネントは変更不要

---

## 認証状態管理

```javascript
// src/frontend/js/utils/auth.js が一元管理

// BAD: 各コンポーネントからlocalStorageに直接アクセス
const token = localStorage.getItem('token');

// GOOD: auth.js経由
import { auth } from '../utils/auth.js';
const token = auth.getToken();
const isLoggedIn = auth.isAuthenticated();

// 401レスポンス時の自動リダイレクト → api-client.js で処理済み
// 各ページ・コンポーネントでの個別処理は不要
```

---

## フィルタ条件とURLパラメータ

```javascript
// フィルタ条件はURLクエリパラメータに反映する（ブックマーク・共有可能にする）
const params = new URLSearchParams({ month: '2026-03', category: '食費' });
history.pushState({}, '', `?${params}`);

// ページロード時にURLパラメータを読み込む
const params = new URLSearchParams(location.search);
const month = params.get('month') || getCurrentMonth();
```

---

## FinFlow固有のUX要件

| 要件 | 詳細 |
|------|------|
| 日付デフォルト | フォームの日付フィールドは今日の日付をデフォルト設定 |
| エラーメッセージ | 「エラーが発生しました」は禁止。具体的な内容を表示する |
| 確認ダイアログ | `alert()`/`confirm()`/`prompt()` 禁止（カスタムUIを使う） |
| サイドバー | モバイルではハンバーガーメニューに切り替わる |
| テーブル | モバイルではカード表示に変更する |

---

## FinFlow固有のディレクトリ構成

```
src/frontend/js/
├── app.js              # エントリーポイント
├── router.js           # ルーティング（未認証はloginにリダイレクト）
├── pages/
│   ├── login.js        # ログイン画面
│   ├── register.js     # 登録画面
│   ├── dashboard.js    # ダッシュボード（Sprint 2）
│   ├── expenses.js     # 支出一覧
│   ├── subscriptions.js # サブスク管理（Sprint 2）
│   └── categories.js   # カテゴリ管理（Sprint 2）
├── components/         # ff-プレフィックスのWeb Components
├── utils/
│   ├── api-client.js   # バックエンドとの唯一の境界
│   └── auth.js         # JWT管理（localStorage）
└── mocks/              # Sprint 1で使用するモックAPI
```

---

## 禁止事項（FinFlow固有）

- PLに相談せずにUI仕様（画面レイアウト・フォーム項目）を変更しない
- SE-1・SE-2に直接API仕様を確認しない（PL経由で確認する）
- `ff-` プレフィックスなしでカスタム要素を定義しない
- Sprint 1でモックAPIを使わずに直接バックエンドに依存した実装をしない
