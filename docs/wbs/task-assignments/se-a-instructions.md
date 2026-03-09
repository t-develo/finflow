# SE-A タスク指示書 - バックエンド（支出管理）

**担当者:** SE-A
**役割:** バックエンド開発（支出管理・CSV取込・カテゴリ分類）
**報告先:** PL（メインエージェント）

---

## 担当範囲

SE-Aは以下の機能領域を担当します:

1. **支出管理** - 支出データのCRUD操作
2. **CSV取込** - 銀行CSVファイルのパース・取り込み
3. **カテゴリ管理** - カテゴリマスタのCRUD
4. **自動分類** - キーワードベースの支出カテゴリ自動分類

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

### S1-A-001: 支出CRUD API実装

| 項目 | 内容 |
|------|------|
| **優先度** | 高（クリティカルパス上） |
| **工数** | 2日 |
| **依存** | S0-PL-004（OpenAPI Spec）、S0-PL-006（マイグレーション） |

**概要:**
支出データの登録・取得・更新・削除を行うREST APIを実装する。

**エンドポイント:**

```
POST   /api/expenses          - 支出を登録
GET    /api/expenses          - 支出一覧を取得
GET    /api/expenses/{id}     - 支出詳細を取得
PUT    /api/expenses/{id}     - 支出を更新
DELETE /api/expenses/{id}     - 支出を削除
```

**リクエストボディ（POST/PUT）:**
```json
{
  "amount": 1500,
  "categoryId": 1,
  "date": "2026-03-08",
  "description": "コンビニ 昼食",
  "memo": "任意のメモ"
}
```

**レスポンス（GET）:**
```json
{
  "id": 1,
  "amount": 1500,
  "categoryId": 1,
  "categoryName": "食費",
  "date": "2026-03-08",
  "description": "コンビニ 昼食",
  "memo": "任意のメモ",
  "createdAt": "2026-03-08T10:30:00Z",
  "updatedAt": "2026-03-08T10:30:00Z"
}
```

**バリデーション:**
- `amount`: 必須、正の数値、小数点第2位まで
- `categoryId`: 必須、存在するカテゴリIDであること
- `date`: 必須、有効な日付形式
- `description`: 必須、1〜200文字

**制約:**
- ユーザーごとのデータ分離（JWT認証から取得したユーザーIDを使用）
- 一覧取得時はデフォルトで日付降順ソート
- 削除は物理削除（論理削除は今回スコープ外）

**完了条件:**
- [ ] 全5エンドポイントが正常動作
- [ ] バリデーションエラー時に適切なエラーレスポンス（400 Bad Request）
- [ ] 存在しないID指定時に404 Not Found
- [ ] Swagger UIで動作確認可能
- [ ] 単体テスト3件以上がパス

---

### S1-A-002: カテゴリマスタCRUD API

| 項目 | 内容 |
|------|------|
| **優先度** | 高 |
| **工数** | 1日 |
| **依存** | S0-PL-006（マイグレーション） |

**概要:**
支出カテゴリのマスタデータを管理するREST API。初期カテゴリのシードデータも作成する。

**エンドポイント:**

```
POST   /api/categories          - カテゴリを登録
GET    /api/categories          - カテゴリ一覧を取得
PUT    /api/categories/{id}     - カテゴリを更新
DELETE /api/categories/{id}     - カテゴリを削除
```

**リクエストボディ（POST/PUT）:**
```json
{
  "name": "食費",
  "color": "#FF6384",
  "icon": "utensils"
}
```

**初期シードデータ:**
- 食費、日用品、交通費、光熱費、通信費、娯楽、医療費、教育費、衣服、その他

**バリデーション:**
- `name`: 必須、1〜50文字、ユーザー内で一意
- `color`: 任意、CSSカラーコード形式（#RRGGBB）
- `icon`: 任意、アイコン名文字列

**制約:**
- システムデフォルトカテゴリ（シードデータ）は削除不可
- 支出に紐づいているカテゴリは削除不可（409 Conflict）

**完了条件:**
- [ ] 全4エンドポイントが正常動作
- [ ] シードデータがマイグレーション時に投入される
- [ ] 削除制約が正しく動作
- [ ] 単体テスト2件以上がパス

---

### S1-A-003: CSVパースエンジン

| 項目 | 内容 |
|------|------|
| **優先度** | 高（クリティカルパス上） |
| **工数** | 1.5日 |
| **依存** | S1-A-001（支出CRUD） |

**概要:**
CSVファイルをパースして支出データに変換するエンジン。アダプタパターンを採用し、銀行ごとのフォーマット差異を吸収する設計とする。

**実装内容:**

1. **インターフェース定義:**
```csharp
public interface ICsvParser
{
    string FormatName { get; }
    bool CanParse(string headerLine);
    IEnumerable<CsvExpenseRow> Parse(Stream csvStream);
}

public record CsvExpenseRow(
    decimal Amount,
    DateTime Date,
    string Description,
    string? Category,
    string? RawLine,
    int LineNumber,
    bool HasError,
    string? ErrorMessage
);
```

2. **GenericCsvParser:** 汎用CSVフォーマット（日付,説明,金額,カテゴリ）をパースする基本実装

3. **CsvParserFactory:** ヘッダー行からフォーマットを自動判定し、適切なパーサーを選択

**制約:**
- 文字エンコーディング: UTF-8, Shift_JIS の両方に対応
- 1ファイルあたり最大10,000行
- パースエラーが発生した行はスキップし、エラー情報を返す（処理は中断しない）
- CSVライブラリ: CsvHelper を使用

**完了条件:**
- [ ] `ICsvParser` インターフェースが定義されている
- [ ] `GenericCsvParser` が正常動作する
- [ ] `CsvParserFactory` がヘッダー行からパーサーを選択できる
- [ ] テスト3件以上（正常パース、異常行スキップ、エンコーディング対応）

---

### S1-A-004: カテゴリ自動分類ロジック

| 項目 | 内容 |
|------|------|
| **優先度** | 中 |
| **工数** | 0.5日 |
| **依存** | S1-A-002（カテゴリマスタCRUD） |

**概要:**
支出の説明文に含まれるキーワードから、カテゴリを自動的に判定するロジック。

**実装内容:**

```csharp
public interface ICategoryClassifier
{
    int? Classify(string description, IEnumerable<ClassificationRule> rules);
}

public record ClassificationRule(
    string Keyword,
    int CategoryId,
    int Priority
);
```

**デフォルト分類ルール例:**
- "コンビニ", "スーパー", "ランチ" → 食費
- "電車", "バス", "タクシー", "Suica" → 交通費
- "電気", "ガス", "水道" → 光熱費
- "Amazon", "楽天" → 日用品（デフォルト）

**制約:**
- キーワードは部分一致で判定
- 複数ルールがマッチした場合は、Priority が高い（数値が小さい）ものを優先
- マッチしない場合は `null` を返す（未分類扱い）

**完了条件:**
- [ ] `ICategoryClassifier` が正常動作
- [ ] デフォルト分類ルールがシードデータとして登録される
- [ ] テスト2件以上（マッチあり、マッチなし）

---

## Sprint 2 タスク概要

Sprint 2では以下のタスクを担当します（詳細はSprint 1完了後にPLから指示）:

| ID | タスク | 工数 |
|----|--------|------|
| S2-A-001 | CSV取込API（POST /api/expenses/import） | 1日 |
| S2-A-002 | 銀行別CSVアダプタ追加（三菱UFJ、楽天カード） | 1.5日 |
| S2-A-003 | 自動分類ルールCRUD API | 1日 |
| S2-A-004 | 支出検索・フィルタAPI | 1日 |
| S2-A-005 | Sprint 1バグ修正 | 0.5日 |

---

## ディレクトリ構造（SE-A担当箇所）

```
src/
└── FinFlow.Api/
    └── Controllers/
        ├── ExpensesController.cs       ← S1-A-001
        └── CategoriesController.cs     ← S1-A-002
└── FinFlow.Domain/
    └── Entities/
        ├── Expense.cs
        └── Category.cs
    └── Interfaces/
        ├── ICsvParser.cs               ← S1-A-003
        └── ICategoryClassifier.cs      ← S1-A-004
└── FinFlow.Infrastructure/
    └── Services/
        ├── CsvParsing/
        │   ├── GenericCsvParser.cs      ← S1-A-003
        │   └── CsvParserFactory.cs     ← S1-A-003
        └── CategoryClassifier.cs       ← S1-A-004
tests/
└── FinFlow.Tests/
    ├── ExpensesControllerTests.cs
    ├── CategoriesControllerTests.cs
    ├── CsvParserTests.cs
    └── CategoryClassifierTests.cs
```
