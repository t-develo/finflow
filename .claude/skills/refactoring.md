# リファクタリング共通語彙【汎用】

チーム全体で共通言語でリファクタリングを議論するための参照資料。
レビュー指摘・SE間会話・PL判断に使用する。

---

## リファクタリングのタイミング（いつやるか）

| タイミング | 説明 |
|-----------|------|
| **準備のため** | 機能追加前に、追加しやすいよう既存コードを整える |
| **理解のため** | コードを読み解く過程で、より分かりやすい構造に書き直す |
| **ゴミ拾い（ボーイスカウトルール）** | 触ったついでに小さな改善をする |
| **Rule of Three** | 同じパターンが3回出たら抽象化を検討する |

**重要:** リファクタリングとフィーチャー追加は**別コミット**にする。

---

## Code Smells（不吉な匂い）一覧

| Code Smell | 症状 | 対処 |
|------------|------|------|
| **長いメソッド（Long Method）** | 20行超 | Extract Method |
| **長いパラメータリスト（Long Parameter List）** | 引数3個超 | Introduce Parameter Object |
| **変更の偏り（Divergent Change）** | 1クラスが複数の理由で変更される | Extract Class |
| **変更の分散（Shotgun Surgery）** | 1つの変更が複数クラスに波及 | Move Method / Move Field |
| **データの群れ（Data Clumps）** | いつも一緒に渡されるデータ | Introduce Parameter Object |
| **基本データ型への執着（Primitive Obsession）** | ドメイン概念が基本型のまま | Replace Primitive with Object |
| **スイッチ文の頻出（Switch Statements）** | 同じ条件分岐が複数箇所に | Replace Conditional with Polymorphism |
| **怠け者クラス（Lazy Class）** | 存在意義が薄いクラス | Inline Class |
| **特性の横恋慕（Feature Envy）** | 他クラスのデータを多用するメソッド | Move Method |
| **不適切な関係（Inappropriate Intimacy）** | 2クラスが互いの内部を過度に参照 | Move Method / Extract Class |
| **コメントが臭い（Comments as Deodorant）** | コメントで補わないと理解できない | コード自体を改善 |

---

## リファクタリング手法リファレンス（Martin Fowler）

レビュー指摘では以下の**正式な手法名**を使用する。

### メソッド整理

| 手法名 | 使い所 |
|--------|--------|
| **Extract Method** | 長いメソッドの分割、コメントで区切られたブロック |
| **Inline Method** | 中身が自明なメソッドの除去 |
| **Extract Variable** | 複雑な条件式・計算に名前を付ける |
| **Replace Temp with Query** | 一時変数をメソッド/プロパティ化 |

### オブジェクト整理

| 手法名 | 使い所 |
|--------|--------|
| **Move Method** | 他クラスのデータを多用するメソッドを移動 |
| **Extract Class** | 1クラスの責務過多を分割 |
| **Inline Class** | 存在意義の薄いクラスを統合 |

### データ構造

| 手法名 | 使い所 |
|--------|--------|
| **Introduce Parameter Object** | 複数の引数をオブジェクトにまとめる |
| **Replace Primitive with Object** | 金額・住所等のドメイン概念を値オブジェクト化 |
| **Encapsulate Collection** | コレクションプロパティの直接公開を防ぐ |

### 条件分岐

| 手法名 | 使い所 |
|--------|--------|
| **Replace Conditional with Polymorphism** | if/switchの連鎖をポリモーフィズムに |
| **Introduce Special Case** | null/空チェックの散在を統一（Null Object） |
| **Decompose Conditional** | 複雑な条件式を分割・名前付け |

### API設計

| 手法名 | 使い所 |
|--------|--------|
| **Separate Query from Modifier** | コマンドとクエリの分離（CQS原則） |
| **Hide Delegate** | 委譲先の隠蔽（デメテルの法則） |

---

## 達人プログラマー観点でのリファクタリング判断

| 観点 | 問い |
|------|------|
| **ETC（Easy To Change）** | この設計は将来の変更を容易にするか |
| **直交性** | 変更が関係ない他のモジュールに波及しないか |
| **DRYの正しい適用** | 知識（ビジネスルール）の重複が排除されているか |
| **割れ窓** | 既存の品質基準を下げるコードが含まれていないか |

---

## レビューでの指摘例

```
Should Fix: UserService.cs:45 - Long Method
このメソッドは42行あり、入力検証・ビジネスロジック・DB保存の3責務を担っています。
→ Extract Method で ValidateInput / ProcessLogic / SaveAsync に分割してください。

Must Fix: ReportController.cs:78 - Primitive Obsession
金額計算にdouble型を使用しています。精度誤差が発生する可能性があります。
→ decimalに変更してください。

Nit: DashboardPage.js:120 - Feature Envy
このメソッドはUserServiceのデータを多用しています。
→ Move Method でUserServiceへの移動を検討してください。
```
