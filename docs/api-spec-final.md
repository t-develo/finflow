# FinFlow API 仕様書 最終版

**バージョン:** 1.0.0
**作成日:** 2026-03-12
**ベースURL:** `https://your-domain.com/api`

---

## 概要

FinFlow API は ASP.NET Core Web API で実装されています。すべてのエンドポイント（認証エンドポイントを除く）は JWT Bearer 認証が必要です。

### 認証

```
Authorization: Bearer <JWT-TOKEN>
```

JWT トークンは `POST /api/auth/login` または `POST /api/auth/register` で取得します。
トークンの有効期限はデフォルト 24 時間です。

### 共通レスポンス形式

**エラーレスポンス:**
```json
{
  "error": "エラーメッセージ",
  "statusCode": 400
}
```

**HTTPステータスコード:**

| コード | 意味 |
|--------|------|
| 200 | 成功 |
| 201 | 作成成功 |
| 204 | 削除成功（ボディなし） |
| 400 | バリデーションエラー |
| 401 | 認証失敗（トークン無効・期限切れ） |
| 403 | アクセス拒否 |
| 404 | リソースが見つからない |
| 409 | 競合（重複など） |
| 500 | サーバー内部エラー |

---

## 認証 (`/api/auth`)

### POST /api/auth/register

新規ユーザーを登録します。

**認証:** 不要

**リクエストボディ:**
```json
{
  "email": "user@example.com",
  "password": "MyPassword1"
}
```

| フィールド | 型 | 必須 | 説明 |
|------------|-----|------|------|
| email | string | 必須 | メールアドレス（フォーマット検証あり） |
| password | string | 必須 | パスワード（8文字以上、数字を含む） |

**成功レスポンス (200):**
```json
{
  "token": "eyJhbGci...",
  "userId": "abc123",
  "email": "user@example.com",
  "expiresAt": "2026-03-13T09:00:00Z"
}
```

**エラーレスポンス (400):**
```json
{
  "errors": ["Passwords must have at least one digit ('0'-'9')."]
}
```

---

### POST /api/auth/login

既存ユーザーでログインします。

**認証:** 不要

**リクエストボディ:**
```json
{
  "email": "user@example.com",
  "password": "MyPassword1"
}
```

**成功レスポンス (200):**
```json
{
  "token": "eyJhbGci...",
  "userId": "abc123",
  "email": "user@example.com",
  "expiresAt": "2026-03-13T09:00:00Z"
}
```

**エラーレスポンス (401):**
```json
{
  "error": "Invalid email or password."
}
```

---

## 支出 (`/api/expenses`)

すべてのエンドポイントで JWT 認証が必要です。データはログインユーザーのみに限定されます。

### GET /api/expenses

支出一覧を取得します（ページネーション付き）。

**クエリパラメーター:**

| パラメーター | 型 | 必須 | デフォルト | 説明 |
|------------|-----|------|----------|------|
| from | string | - | - | 開始日（YYYY-MM-DD） |
| to | string | - | - | 終了日（YYYY-MM-DD） |
| categoryId | integer | - | - | カテゴリIDでフィルター |
| minAmount | decimal | - | - | 最小金額 |
| maxAmount | decimal | - | - | 最大金額 |
| keyword | string | - | - | 説明のキーワード検索 |
| page | integer | - | 1 | ページ番号 |
| pageSize | integer | - | 50 | 1ページあたり件数 |

**成功レスポンス (200):**
```json
{
  "data": [
    {
      "id": 1,
      "amount": 3500,
      "categoryId": 1,
      "categoryName": "食費",
      "date": "2026-03-01",
      "description": "スーパーマーケット",
      "createdAt": "2026-03-01T10:00:00Z",
      "updatedAt": "2026-03-01T10:00:00Z"
    }
  ],
  "pagination": {
    "page": 1,
    "pageSize": 50,
    "hasNextPage": false
  }
}
```

---

### GET /api/expenses/{id}

指定 ID の支出を取得します。

**パスパラメーター:** `id` (integer)

**成功レスポンス (200):** 支出オブジェクト（上記と同形式）

**エラーレスポンス (404):** リソースが見つからない場合

---

### POST /api/expenses

新規支出を作成します。

**リクエストボディ:**
```json
{
  "amount": 3500,
  "categoryId": 1,
  "date": "2026-03-01",
  "description": "スーパーマーケット",
  "memo": "任意のメモ"
}
```

| フィールド | 型 | 必須 | 説明 |
|------------|-----|------|------|
| amount | decimal | 必須 | 金額（0.01 以上） |
| categoryId | integer | 必須 | カテゴリID |
| date | string | 必須 | 日付（YYYY-MM-DD） |
| description | string | 必須 | 説明（1〜200文字） |
| memo | string | - | メモ（500文字以内） |

**成功レスポンス (201):** 作成された支出オブジェクト

---

### PUT /api/expenses/{id}

支出を更新します。

**パスパラメーター:** `id` (integer)

**リクエストボディ:** POST と同じ

**成功レスポンス (200):** 更新された支出オブジェクト

**エラーレスポンス (404):** リソースが見つからない場合

---

### DELETE /api/expenses/{id}

支出を削除します。

**パスパラメーター:** `id` (integer)

**成功レスポンス (204):** ボディなし

**エラーレスポンス (404):** リソースが見つからない場合

---

### POST /api/expenses/import

CSV ファイルから支出を一括インポートします。

**リクエスト:** `multipart/form-data`

| フィールド | 型 | 必須 | 説明 |
|------------|-----|------|------|
| file | file | 必須 | CSV ファイル |
| bankFormat | string | - | フォーマット指定（省略時は自動検出） |

**成功レスポンス (200):**
```json
{
  "imported": 10,
  "skipped": 2,
  "errors": [
    "Row 3: Invalid date format: '2026/13/01'",
    "Row 7: Amount must be positive: '-100'"
  ]
}
```

---

## カテゴリ (`/api/categories`)

### GET /api/categories

カテゴリ一覧を取得します（システムカテゴリ + ユーザーカテゴリ）。

**成功レスポンス (200):**
```json
[
  {
    "id": 1,
    "name": "食費",
    "color": "#FF6384",
    "isSystem": true,
    "createdAt": "2026-01-01T00:00:00Z",
    "updatedAt": "2026-01-01T00:00:00Z"
  }
]
```

---

### GET /api/categories/{id}

指定 ID のカテゴリを取得します。

---

### POST /api/categories

新規カテゴリを作成します。

**リクエストボディ:**
```json
{
  "name": "交際費",
  "color": "#36A2EB"
}
```

| フィールド | 型 | 必須 | 説明 |
|------------|-----|------|------|
| name | string | 必須 | カテゴリ名（1〜100文字） |
| color | string | - | 16進カラーコード（例: #FF6384）、デフォルト #6B7280 |

**成功レスポンス (201):** 作成されたカテゴリオブジェクト

**エラーレスポンス (409):** 同名カテゴリが既に存在する場合

---

### PUT /api/categories/{id}

カテゴリを更新します（ユーザーカテゴリのみ。システムカテゴリは更新不可）。

---

### DELETE /api/categories/{id}

カテゴリを削除します（ユーザーカテゴリのみ。支出が紐づいている場合は削除不可）。

---

## サブスクリプション (`/api/subscriptions`)

### GET /api/subscriptions

サブスクリプション一覧を取得します（次回請求日順）。

**成功レスポンス (200):**
```json
[
  {
    "id": 1,
    "serviceName": "Netflix",
    "amount": 1490,
    "categoryId": 5,
    "categoryName": "娯楽・趣味",
    "billingCycle": "monthly",
    "nextBillingDate": "2026-03-15",
    "description": "スタンダードプラン",
    "isActive": true,
    "createdAt": "2026-01-01T00:00:00Z",
    "updatedAt": "2026-01-01T00:00:00Z"
  }
]
```

---

### GET /api/subscriptions/{id}

指定 ID のサブスクリプションを取得します。

---

### POST /api/subscriptions

新規サブスクリプションを登録します。

**リクエストボディ:**
```json
{
  "serviceName": "Netflix",
  "amount": 1490,
  "categoryId": 5,
  "billingCycle": "monthly",
  "nextBillingDate": "2026-03-15",
  "description": "スタンダードプラン",
  "isActive": true
}
```

| フィールド | 型 | 必須 | 説明 |
|------------|-----|------|------|
| serviceName | string | 必須 | サービス名（1〜100文字） |
| amount | decimal | 必須 | 請求金額（0.01 以上） |
| categoryId | integer | - | カテゴリID |
| billingCycle | string | 必須 | `monthly` / `yearly` / `weekly` |
| nextBillingDate | string | 必須 | 次回請求日（YYYY-MM-DD） |
| description | string | - | メモ |
| isActive | boolean | - | 有効/無効（デフォルト: true） |

---

### PUT /api/subscriptions/{id}

サブスクリプションを更新します。

---

### DELETE /api/subscriptions/{id}

サブスクリプションを削除します。

---

## レポート (`/api/reports`)

### GET /api/reports/monthly

月次支出集計を取得します。

**クエリパラメーター:**

| パラメーター | 型 | 必須 | 説明 |
|------------|-----|------|------|
| year | integer | 必須 | 年（2000〜2099） |
| month | integer | 必須 | 月（1〜12） |

**成功レスポンス (200):**
```json
{
  "year": 2026,
  "month": 3,
  "totalAmount": 85000,
  "totalCount": 42,
  "dailyAverage": 2742,
  "categoryBreakdown": [
    {
      "categoryId": 1,
      "categoryName": "食費",
      "amount": 35000,
      "count": 20,
      "percentage": 41.2
    }
  ]
}
```

---

### GET /api/reports/by-category

カテゴリ別支出集計を取得します（円グラフ用、カラー情報付き）。

**クエリパラメーター:** `year`, `month`（月次レポートと同じ）

**成功レスポンス (200):**
```json
{
  "year": 2026,
  "month": 3,
  "categories": [
    {
      "categoryId": 1,
      "categoryName": "食費",
      "categoryColor": "#FF6384",
      "totalAmount": 35000,
      "count": 20,
      "percentage": 41.2
    }
  ]
}
```

---

### GET /api/reports/monthly/pdf

月次レポートを PDF ファイルとしてダウンロードします。

**クエリパラメーター:** `year`, `month`（月次レポートと同じ）

**成功レスポンス (200):**
- Content-Type: `application/pdf`
- Content-Disposition: `attachment; filename=finflow-report-YYYY-MM.pdf`

---

## ダッシュボード (`/api/dashboard`)

### GET /api/dashboard/summary

ダッシュボード表示用の集約データを取得します。

**成功レスポンス (200):**
```json
{
  "currentMonth": {
    "year": 2026,
    "month": 3,
    "totalAmount": 85000,
    "expenseCount": 42
  },
  "previousMonth": {
    "year": 2026,
    "month": 2,
    "totalAmount": 72000,
    "expenseCount": 38
  },
  "monthOverMonthChange": {
    "amountDiff": 13000,
    "percentageChange": 18.1
  },
  "topCategories": [
    {
      "categoryId": 1,
      "categoryName": "食費",
      "totalAmount": 35000,
      "percentage": 41.2
    }
  ],
  "recentExpenses": [
    {
      "id": 100,
      "amount": 3500,
      "categoryName": "食費",
      "description": "スーパーマーケット",
      "date": "2026-03-12"
    }
  ],
  "upcomingSubscriptions": [
    {
      "id": 1,
      "serviceName": "Netflix",
      "amount": 1490,
      "nextBillingDate": "2026-03-15",
      "daysUntilBilling": 3
    }
  ]
}
```

---

## 自動分類ルール (`/api/classification-rules`)

### GET /api/classification-rules

自動分類ルール一覧を取得します。

**成功レスポンス (200):**
```json
[
  {
    "id": 1,
    "keyword": "スーパー",
    "categoryId": 1,
    "categoryName": "食費",
    "priority": 100,
    "createdAt": "2026-03-01T00:00:00Z",
    "updatedAt": "2026-03-01T00:00:00Z"
  }
]
```

---

### GET /api/classification-rules/{id}

指定 ID の分類ルールを取得します。

---

### POST /api/classification-rules

新規分類ルールを作成します。

**リクエストボディ:**
```json
{
  "keyword": "スーパー",
  "categoryId": 1,
  "priority": 100
}
```

| フィールド | 型 | 必須 | 説明 |
|------------|-----|------|------|
| keyword | string | 必須 | マッチキーワード（1〜200文字） |
| categoryId | integer | 必須 | 割り当てるカテゴリID（1以上） |
| priority | integer | - | 優先度（1〜1000、デフォルト: 100、低い数値 = 高優先） |

---

### PUT /api/classification-rules/{id}

分類ルールを更新します。

---

### DELETE /api/classification-rules/{id}

分類ルールを削除します。

---

## CSV フォーマット仕様

### 汎用フォーマット (Generic)

ヘッダー行に `date`, `description`, `amount` が含まれるファイル。

```csv
date,description,amount,categoryId
2026-03-01,スーパーマーケット,3500,1
2026-03-02,電車代,200,2
```

### MUFG フォーマット

三菱UFJ銀行の明細CSVフォーマットに対応。Shift_JIS エンコーディングをサポート。

### 楽天フォーマット

楽天銀行の明細CSVフォーマットに対応。Shift_JIS エンコーディングをサポート。

### 共通制限

- 最大行数: 10,000 行
- サポートエンコーディング: UTF-8, Shift_JIS (CP932)
- エラー行はスキップ（インポート継続）

---

## セキュリティ仕様

- **認証方式:** JWT Bearer（HMAC-SHA256 署名）
- **JWT 有効期限:** 24 時間（設定変更可）
- **CORS:** 許可オリジンは設定ファイルで管理
- **パスワードポリシー:** 8文字以上、数字を含む
- **SQL インジェクション対策:** EF Core パラメーター化クエリを使用
- **ユーザー分離:** 全データクエリに `UserId` フィルターを適用
