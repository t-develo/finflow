# FinFlow アーキテクチャ原則【全ロール共通】

FinFlowのアーキテクチャ設計・実装・レビューで全ロールが遵守すべき原則をまとめる。

---

## レイヤー構成と依存ルール（Clean Architecture）

```
FinFlow.Domain/         ← 最内層: Entity, Interface（外部依存ゼロ）
FinFlow.Infrastructure/ ← 中間層: EF Core, 外部サービス実装
FinFlow.Api/            ← 最外層: Controller, Middleware, DI設定
```

**依存の方向は常に「外側 → 内側」。絶対に逆転させない。**

| 許可 | 禁止 |
|------|------|
| Api → Infrastructure → Domain | Domain → Infrastructure |
| Api → Domain | Infrastructure → Api |

### 各層の責務

| 層 | 責務 | 禁止事項 |
|----|------|----------|
| **Controller** | HTTPの入出力変換のみ | ビジネスロジック、try-catch |
| **Service** | ビジネスロジック | 直接HTTPリクエスト/レスポンスを扱う |
| **Entity/Domain** | ビジネスルール、不変条件の保護 | EF Core属性以外のインフラ依存 |
| **Repository（DbContext）** | データアクセス | ビジネスロジック |

---

## 設計原則

### KISS / YAGNI / DRY

- **KISS:** シンプルな設計を選ぶ。複雑さは最大の敵
- **YAGNI:** 将来のためだけの過剰設計を禁止する
- **DRY（正しい適用）:** 「コードの重複」ではなく「知識（ビジネスルール）の重複」を排除する。見た目が似ていても文脈が異なれば重複ではない

### SOLID原則の適用

- **S（単一責任）:** 1クラスが変更される理由は1つだけ
- **O（開放閉鎖）:** 拡張に開き、修正に閉じる（例: 新銀行パーサー追加 = 既存コード変更なし）
- **L（リスコフの置換）:** インターフェースの実装は契約を完全に満たす
- **I（インターフェース分離）:** 巨大なインターフェースより目的別の小さなインターフェース
- **D（依存性逆転）:** 具象クラスではなくインターフェースに依存する

### DTOの使用

**EntityをそのままAPIレスポンスに返さない。** 層の境界ではDTOに変換する。

```csharp
// BAD: Entityをそのまま返す
return Ok(expense);

// GOOD: DTOに変換して返す
return Ok(new ExpenseDto { Id = expense.Id, ... });
```

---

## FinFlow固有の設計パターン

### CSVパーサー（アダプタパターン）

```
ICsvParser (Domain/Interfaces/)
├── GenericCsvParser       ← Sprint 1
├── MufgCsvParser          ← Sprint 2
└── RakutenCsvParser       ← Sprint 2

CsvParserFactory → ヘッダー行からParser選択
```

新フォーマット追加 = 新クラス追加のみ、既存コードへの変更なし（OCP）。

### サービス層（SE-1/SE-2の責務境界）

| 担当 | テーブル | 操作 |
|------|----------|------|
| SE-1 | Expense, Category, ClassificationRule | CRUD（所有者） |
| SE-2 | Expense, Category | 読み取り専用（集計） |
| SE-2 | Subscription | CRUD（所有者） |

SE-2がExpense/Categoryのスキーマ変更を必要とする場合は**必ずPLを通す**。

### バックグラウンドサービス（SE-2 Sprint 2）

```
IHostedService
└── NotificationScheduler → ISubscriptionService（インターフェース経由）
```

定期実行ロジックとビジネスロジックを分離する。

### フロントエンド（SE-3）の依存方向

```
[Pages] → [Web Components] → [Utils / api-client.js]
  具体的         汎用的               インフラ層
```

`api-client.js` はバックエンドとの唯一の境界。ページやコンポーネントから直接fetchしない。

---

## アーキテクチャ違反の検出（レビュー観点）

以下が発生したらアーキテクチャ違反として指摘する:

- [ ] `FinFlow.Domain` が `Microsoft.EntityFrameworkCore` を参照している
- [ ] Controller に `if/else` のビジネスロジックが書かれている
- [ ] `DbContext` を Service 層を飛ばして Controller から直接使用している
- [ ] QuestPDF/iText7 等の外部ライブラリのクラスが Domain 層に現れている
- [ ] `new ConcreteService()` でサービスをインスタンス化している（DIを使っていない）
- [ ] フロントエンドのページコンポーネントから `fetch()` を直接呼んでいる
