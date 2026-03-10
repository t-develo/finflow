# FinFlow PL固有ガイド【FinFlow固有】

FinFlowプロジェクト固有のPL管理事項。
汎用的なマネジメント原則は `/pl-management` を参照。

---

## PL直轄の実装担当

PLは管理だけでなく以下の共通基盤を自ら実装する（プレイングマネージャー）:

- ソリューション構成（.slnファイル、プロジェクト参照）
- DB設計・ERD・EF Coreマイグレーション基盤
- JWT認証基盤（ASP.NET Identity + JWT）
- OpenAPI仕様書（Swaggerコメント）
- グローバルエラーハンドリングミドルウェア
- CI/CD設定

---

## SE責務の境界（FinFlow固有）

| 担当 | 所有テーブル | 他SEとの関係 |
|------|------------|-------------|
| **SE-1** | Expense, Category, ClassificationRule | SE-2にExpense/Categoryを読み取り専用で提供 |
| **SE-2** | Subscription | SE-1のExpense/CategoryをAsNoTrackingで集計 |
| **SE-3** | フロントエンドのみ | Sprint 1: モックAPI Sprint 2: 実API |
| **PL** | User（ASP.NET Identity） | 全SEに共通基盤を提供 |

---

## クリティカルパスの管理

**SE-1がクリティカルパス上にある。** SE-1の遅延は次を引き起こす:

```
SE-1遅延 → Expense/Category APIが未完成
         → SE-2の集計機能がブロック
         → SE-3がモックAPIへの依存を続けざるを得ない
```

SE-1の進捗を週次で確認し、ブロッカーを優先解消する。

---

## SE間の連携ルール（FinFlow固有）

### SE-2がExpense/Categoryスキーマ変更を必要とする場合

```
SE-2 → PLに相談 → PLがSE-1に依頼 → SE-1が対応
```
**SE-2がSE-1に直接変更依頼することは禁止。**

### SE-3がAPIの疑問点を持った場合

```
SE-3 → PLに相談 → PLが該当SEに確認 → SE-3に回答
```
SE-3がSE-1/SE-2に直接確認することは禁止（仕様の齟齬を防ぐ）。

---

## ライブラリ選定の確認事項

PDF出力ライブラリはSprint 1中に確定し、SE-2のSprint 2作業開始前に通知する。

| 候補 | 特徴 |
|------|------|
| **QuestPDF** | C#ネイティブ、流暢なAPI、商用利用は要ライセンス確認 |
| **iText7** | 豊富な機能、AGPLライセンス |

---

## レビュアーとの連携（FinFlow固有の依頼事項）

FinFlowの場合、レビュー依頼時に特に以下を明示する:

```markdown
### 特に注意して見てほしい点
- [ ] Expense/Category/SubscriptionクエリのUserId分離
- [ ] 金額計算のdecimal型使用
- [ ] CSVパーサーのインジェクション防止（SE-1タスクのみ）
- [ ] api-client.js経由でのAPI呼び出し（SE-3タスクのみ）
```

---

## Sprint構成の概要

| Sprint | 主な成果物 |
|--------|-----------|
| **Sprint 1** | 認証基盤、Expense/Category CRUD、フロントSPA基盤（モックAPI） |
| **Sprint 2** | CSV取込、集計・PDF、通知、全フロント画面（実API切り替え） |
