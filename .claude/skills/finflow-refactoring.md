# FinFlow リファクタリング共通語彙【全ロール共通】

チーム全体で共通の言語でリファクタリングについて話すための参照資料。
レビューでの指摘、SE間の会話、PL判断に使用する。

---

## リファクタリングのタイミング（いつやるか）

| タイミング | 説明 | 例 |
|-----------|------|-----|
| **準備のため** | 機能追加前に、追加しやすいよう整える | 新レポート追加前に既存集計ロジックを整理 |
| **理解のため** | コードを読み解く過程で書き直す | SE-1の作ったEntityを読みながら理解を深める |
| **ゴミ拾い（ボーイスカウトルール）** | 触ったついでに小さな改善 | 通りかかったメソッドの命名を修正 |
| **Rule of Three** | 同じパターンが3回出たら抽象化 | 3つのページに同じフィルタロジック → 共通化 |

**重要:** リファクタリングとフィーチャー追加は**別コミット**にする。帽子を被り替える意識を持つ。

---

## Code Smells（不吉な匂い）一覧

以下の匂いを検出したらリファクタリングを検討する。

| Code Smell | 症状 | 対処 |
|------------|------|------|
| **長いメソッド（Long Method）** | 20行超 | Extract Method |
| **長いパラメータリスト（Long Parameter List）** | 引数3個超 | Introduce Parameter Object |
| **変更の偏り（Divergent Change）** | 1クラスが複数の理由で変更される | Extract Class |
| **変更の分散（Shotgun Surgery）** | 1つの変更が複数クラスに波及 | Move Method / Move Field |
| **データの群れ（Data Clumps）** | いつも一緒に渡されるデータ | Introduce Parameter Object |
| **基本データ型への執着（Primitive Obsession）** | 金額がdecimalのまま渡し回される | Replace Primitive with Object |
| **スイッチ文の頻出（Switch Statements）** | 同じ条件分岐が複数箇所に | Replace Conditional with Polymorphism |
| **怠け者クラス（Lazy Class）** | 存在意義が薄いクラス | Inline Class |
| **特性の横恋慕（Feature Envy）** | 他クラスのデータを多用するメソッド | Move Method |
| **不適切な関係（Inappropriate Intimacy）** | 2クラスが互いの内部を過度に参照 | Move Method / Extract Class |
| **コメントが臭い（Comments as Deodorant）** | コメントで補わないと理解できない | コード自体を改善 |

---

## リファクタリング手法リファレンス

レビュー指摘・SE間の会話では以下の**正式な手法名**を使用する。

### メソッド整理

| 手法名 | 使い所 | FinFlow例 |
|--------|--------|-----------|
| **Extract Method** | 長いメソッドの分割 | CSVパース処理からバリデーション部分を切り出す |
| **Inline Method** | 中身が自明なメソッドの除去 | 1行しかない委譲メソッドを除去 |
| **Extract Variable** | 複雑な条件式に名前を付ける | `isWithinDateRange && belongsToUser && isActive` |
| **Replace Temp with Query** | 一時変数をメソッド化 | 合計金額の計算を `TotalAmount` プロパティに |

### オブジェクト整理

| 手法名 | 使い所 | FinFlow例 |
|--------|--------|-----------|
| **Move Method** | 責務の正しいクラスへ移動 | ControllerにあるロジックをServiceへ移動 |
| **Extract Class** | 1クラスの責務過多を分割 | ReportServiceからPDF生成を分離 |
| **Inline Class** | 存在意義の薄いクラスの統合 | 何もしないラッパークラスの除去 |

### データ構造

| 手法名 | 使い所 | FinFlow例 |
|--------|--------|-----------|
| **Introduce Parameter Object** | 多すぎる引数をまとめる | 年月・UserId・カテゴリをReportQueryオブジェクトに |
| **Replace Primitive with Object** | 基本型を値オブジェクトに | decimal金額 → Money値オブジェクト（必要に応じて） |
| **Encapsulate Collection** | コレクションの直接公開を防ぐ | Listプロパティの読み取り専用化 |

### 条件分岐

| 手法名 | 使い所 | FinFlow例 |
|--------|--------|-----------|
| **Replace Conditional with Polymorphism** | if/switchの連鎖をポリモーフィズムに | 銀行別パーサーの選択 → Factory + Strategy |
| **Introduce Special Case** | null/空チェックの散在を統一 | `EmptyReportResult`で0件を統一的に扱う |
| **Decompose Conditional** | 複雑な条件式を分割 | ガード節で異常系を早期リターン |

### API設計

| 手法名 | 使い所 | FinFlow例 |
|--------|--------|-----------|
| **Separate Query from Modifier** | コマンドとクエリの分離（CQS） | データ取得メソッドと更新メソッドを分ける |
| **Hide Delegate** | 委譲先の隠蔽（デメテルの法則） | Controller経由でServiceの内部を直接叩かせない |

---

## 指摘例（レビューでの使い方）

```
Should Fix: ExpenseService.cs:45 - Long Method
このメソッドは42行あり、CSV解析・バリデーション・DB保存の3責務を担っています。
→ Extract Method で ParseCsvRows / ValidateExpenses / SaveExpenses に分割してください。

Must Fix: ReportService.cs:120 - Primitive Obsession
金額計算にdouble型を使用しています。精度誤差が発生する可能性があります。
→ Replace Primitive with Object または単純にdecimalに変更してください。

Nit: DashboardController.cs:78 - Feature Envy
このメソッドはReportServiceのデータを多用しています。
→ Move Method でReportServiceへの移動を検討してください。
```
