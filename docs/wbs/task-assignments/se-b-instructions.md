# SE-B タスク指示書 - バックエンド（サブスク管理・レポート）

**担当者:** SE-B
**役割:** バックエンド開発（サブスクリプション管理・集計・通知・レポート生成）
**報告先:** PL（メインエージェント）

---

## 担当範囲

SE-Bは以下の機能領域を担当します:

1. **サブスクリプション管理** - 定期支払いのCRUD操作
2. **集計・レポート** - 月次集計、カテゴリ別集計、ダッシュボードサマリ
3. **通知機能** - サブスク更新日の事前通知（Sprint 2）
4. **PDF生成** - 月次レポートのPDF出力（Sprint 2）

---

## 共通ルール

### コーディング規約
- C# コーディング規約に準拠（PascalCase for public, camelCase for private）
- 非同期メソッドには `Async` サフィックスを付与
- Entity Framework Coreを使用したリポジトリパターン
- コントローラーは薄く保ち、ビジネスロジックはサービス層に配置

### テストルール
- 各APIに対して最低限のテストケースを作成:
  - 正常系: 1件以上
  - 異常系（バリデーションエラー）: 1件以上
  - エッジケース: 可能な限り
- テストプロジェクト: `FinFlow.Tests`
- テストフレームワーク: xUnit + FluentAssertions

### 報告ルール
- 各タスク完了時にPLへ完了報告（実装内容サマリ + テスト結果）
- ブロッカー発生時は即座にPLへ報告
- API仕様の変更が必要な場合は、実装前にPLへ相談

---

## Sprint 1 タスク詳細

### S1-B-001: サブスクリプションCRUD API

| 項目 | 内容 |
|------|------|
| **優先度** | 高 |
| **工数** | 2日 |
| **依存** | S0-PL-004（OpenAPI Spec）、S0-PL-006（マイグレーション） |

**概要:**
サブスクリプション（定期支払い）の登録・取得・更新・削除を行うREST API。

**エンドポイント:**

```
POST   /api/subscriptions          - サブスクを登録
GET    /api/subscriptions          - サブスク一覧を取得
GET    /api/subscriptions/{id}     - サブスク詳細を取得
PUT    /api/subscriptions/{id}     - サブスクを更新
DELETE /api/subscriptions/{id}     - サブスクを削除
```

**リクエストボディ（POST/PUT）:**
```json
{
  "serviceName": "Netflix",
  "amount": 1490,
  "categoryId": 6,
  "billingCycle": "monthly",
  "nextBillingDate": "2026-04-01",
  "description": "スタンダードプラン",
  "isActive": true
}
```

**レスポンス（GET）:**
```json
{
  "id": 1,
  "serviceName": "Netflix",
  "amount": 1490,
  "categoryId": 6,
  "categoryName": "娯楽",
  "billingCycle": "monthly",
  "nextBillingDate": "2026-04-01",
  "description": "スタンダードプラン",
  "isActive": true,
  "createdAt": "2026-03-08T10:00:00Z",
  "updatedAt": "2026-03-08T10:00:00Z"
}
```

**billingCycle の値:**
- `monthly` - 毎月
- `yearly` - 毎年
- `weekly` - 毎週（任意対応）

**バリデーション:**
- `serviceName`: 必須、1〜100文字
- `amount`: 必須、正の数値
- `billingCycle`: 必須、上記enumのいずれか
- `nextBillingDate`: 必須、有効な日付
- `categoryId`: 必須、存在するカテゴリID

**制約:**
- ユーザーごとのデータ分離
- 一覧取得時は `nextBillingDate` 昇順（直近のものが先頭）
- `isActive: false` のサブスクも取得可能（クエリパラメータで絞り込み可）

**完了条件:**
- [ ] 全5エンドポイントが正常動作
- [ ] バリデーションエラー時に400 Bad Request
- [ ] 存在しないID指定時に404 Not Found
- [ ] Swagger UIで動作確認可能
- [ ] 単体テスト3件以上がパス

---

### S1-B-002: 月次集計API

| 項目 | 内容 |
|------|------|
| **優先度** | 高 |
| **工数** | 1.5日 |
| **依存** | S0-PL-006（マイグレーション） |

**概要:**
指定した年月の支出を集計し、合計金額・件数・カテゴリ別内訳を返すAPI。

**エンドポイント:**

```
GET /api/reports/monthly?year=2026&month=3
```

**レスポンス:**
```json
{
  "year": 2026,
  "month": 3,
  "totalAmount": 185000,
  "totalCount": 42,
  "dailyAverage": 5968,
  "categoryBreakdown": [
    {
      "categoryId": 1,
      "categoryName": "食費",
      "amount": 65000,
      "count": 20,
      "percentage": 35.1
    },
    {
      "categoryId": 4,
      "categoryName": "光熱費",
      "amount": 25000,
      "count": 3,
      "percentage": 13.5
    }
  ]
}
```

**制約:**
- `year`: 必須、2000〜2099の範囲
- `month`: 必須、1〜12の範囲
- データがない場合は `totalAmount: 0`, `totalCount: 0` で空配列を返す
- カテゴリ別内訳は金額降順でソート
- `percentage` は小数点第1位まで（四捨五入）

**完了条件:**
- [ ] 正しい集計結果が返される
- [ ] データなしの月で正常なレスポンスが返される
- [ ] カテゴリ別のパーセンテージが正確
- [ ] 単体テスト3件以上がパス

---

### S1-B-003: カテゴリ別集計API

| 項目 | 内容 |
|------|------|
| **優先度** | 中 |
| **工数** | 1日 |
| **依存** | S1-B-002（月次集計API） |

**概要:**
指定期間のカテゴリ別支出集計を返すAPI。ダッシュボードの円グラフ用データ。

**エンドポイント:**

```
GET /api/reports/by-category?year=2026&month=3
```

**レスポンス:**
```json
{
  "year": 2026,
  "month": 3,
  "categories": [
    {
      "categoryId": 1,
      "categoryName": "食費",
      "color": "#FF6384",
      "totalAmount": 65000,
      "count": 20,
      "percentage": 35.1
    }
  ]
}
```

**制約:**
- 月次集計APIの `categoryBreakdown` を拡張し、色情報を含めて返す
- 金額降順ソート
- 集計ロジックは月次集計と共通化（サービス層で共有）

**完了条件:**
- [ ] カテゴリ別集計が正しく返される
- [ ] 色情報（color）がカテゴリマスタから取得されている
- [ ] 単体テスト2件以上がパス

---

### S1-B-004: ダッシュボード用サマリAPI

| 項目 | 内容 |
|------|------|
| **優先度** | 中 |
| **工数** | 0.5日 |
| **依存** | S1-B-002（月次集計API） |

**概要:**
ダッシュボード画面に必要な情報を1つのAPIで返す。フロントエンド（SE-C）のダッシュボード画面が使用する。

**エンドポイント:**

```
GET /api/dashboard/summary
```

**レスポンス:**
```json
{
  "currentMonth": {
    "year": 2026,
    "month": 3,
    "totalAmount": 185000,
    "totalCount": 42
  },
  "previousMonth": {
    "year": 2026,
    "month": 2,
    "totalAmount": 172000,
    "totalCount": 38
  },
  "monthOverMonthChange": {
    "amountDiff": 13000,
    "percentageChange": 7.6
  },
  "topCategories": [
    {
      "categoryId": 1,
      "categoryName": "食費",
      "amount": 65000,
      "percentage": 35.1
    }
  ],
  "recentExpenses": [
    {
      "id": 42,
      "amount": 1500,
      "categoryName": "食費",
      "description": "コンビニ 昼食",
      "date": "2026-03-08"
    }
  ],
  "upcomingSubscriptions": [
    {
      "id": 1,
      "serviceName": "Netflix",
      "amount": 1490,
      "nextBillingDate": "2026-04-01",
      "daysUntilBilling": 24
    }
  ]
}
```

**制約:**
- `topCategories`: 上位5カテゴリ
- `recentExpenses`: 直近5件
- `upcomingSubscriptions`: 今後30日以内に更新日を迎えるアクティブなサブスク
- `monthOverMonthChange.percentageChange`: 前月比の増減率（%）

**完了条件:**
- [ ] 全データが正しく集約されて返される
- [ ] 前月データがない場合のハンドリング（`previousMonth: null`）
- [ ] 単体テスト2件以上がパス

---

## Sprint 2 タスク概要

Sprint 2では以下のタスクを担当します（詳細はSprint 1完了後にPLから指示）:

| ID | タスク | 工数 |
|----|--------|------|
| S2-B-001 | 通知スケジューラ（IHostedService） | 1.5日 |
| S2-B-002 | メール通知送信機能 | 1日 |
| S2-B-003 | PDF月次レポート生成 | 2日 |
| S2-B-004 | Sprint 1バグ修正 | 0.5日 |

---

## ディレクトリ構造（SE-B担当箇所）

```
src/
└── FinFlow.Api/
    └── Controllers/
        ├── SubscriptionsController.cs  ← S1-B-001
        ├── ReportsController.cs        ← S1-B-002, S1-B-003
        └── DashboardController.cs      ← S1-B-004
└── FinFlow.Domain/
    └── Entities/
        ├── Subscription.cs
        └── ClassificationRule.cs
    └── Interfaces/
        ├── IReportService.cs
        └── INotificationService.cs     ← Sprint 2
└── FinFlow.Infrastructure/
    └── Services/
        ├── ReportService.cs            ← S1-B-002, S1-B-003
        ├── DashboardService.cs         ← S1-B-004
        ├── NotificationScheduler.cs    ← Sprint 2
        ├── EmailSender.cs              ← Sprint 2
        └── PdfReportGenerator.cs       ← Sprint 2
tests/
└── FinFlow.Tests/
    ├── SubscriptionsControllerTests.cs
    ├── ReportServiceTests.cs
    └── DashboardServiceTests.cs
```

---

## SE-A との連携ポイント

- **支出データ（Expense）テーブル:** SE-AがCRUDを担当。SE-Bは集計時にこのテーブルを読み取る。
- **カテゴリ（Category）テーブル:** SE-Aが管理。SE-Bはカテゴリ名・色の取得に使用。
- **重要:** Expense/Categoryエンティティの変更が発生した場合は、SE-Aと事前に調整すること。不明点はPLに相談。
