# FinFlow Session State

> このファイルはiOS Claude Codeのセッション間でコンテキストを引き継ぐために使用します。
> セッション開始時に自動的に読み込まれます。セッション中に作業状況を更新してください。

## 最終更新

- **日時:** 2026-07-27
- **担当者/エージェント:** Claude Code（サブスク二重登録 / 削除UI / スマホデザイン最適化）

## 現在のスプリント・マイルストーン

- **スプリント:** Sprint 3 相当（機能は一通り実装済み、品質・回帰テスト・運用整備フェーズ）
- **フォーカス:** ラズパイ常駐運用中に実機で見つかった不具合の修正
- **ブランチ:** `claude/subscription-duplicate-delete-ui-g2s2sh`（`main` = `d114576` から分岐）

## 直前の作業内容

### 完了したタスク（F: サブスク二重登録 / 削除UI / スマホデザイン — 本ブランチ）

実機で報告された 2 件、**「サブスクが二重・三重に登録される」と
「削除ボタンだけ反応が鈍い」は同じ根っこ**だった。

#### 原因1: 再描画されない要素へのリスナー累積（二重登録の正体）

`loadAndRender()` が呼ばれるたびに `attachEventListeners()` を呼び直していたが、
リスナーの貼り先（`#modal-save-btn` 等）は `buildShell()` が 1 度だけ作る
**永続要素**なので、古いリスナーが解除されずに積み上がる。
しかも増えたリスナーがそれぞれ `loadAndRender()` を呼ぶため、
リスナー数は **1 → 2 → 4 → 8 と指数的に増殖**し、POST 回数がそのまま登録件数になった。

| 操作 | 発火前リスナー数 | POST 回数 |
|---|---|---|
| 1 件目の保存 | 1 | 1（正常） |
| 2 件目の保存 | 2 | **2（二重）** |
| 3 件目の保存 | 4 | **4（四重）** |

> **`btn.disabled = true` では防げない。** 1 回のクリックのディスパッチは、
> 途中で `disabled` にしても**その要素に登録済みの全リスナーを最後まで呼ぶ**。
> 人間の二重タップには効くが、多重リスナーには無力。

対策: `attachShellListeners()` を描画時に **1 回だけ**呼ぶ構造へ。一覧は
`#subscription-content` への**イベント委譲**（委譲元が永続なのでリスナーは 1 本）。
保険として `state.busy` の多重送信ガード。`expense-list-page.js` の同型バグも修正。

#### 原因2: `innerHTML` による退避・復元がリスナーを殺していた（削除の正体）

`showDeleteConfirm()` が `.modal__footer` の innerHTML を文字列で退避し、
文字列から復元していた。**復元後の要素はリスナーを 1 本も持たない別物**になるため、
「削除 → キャンセル」を一度でも操作すると、以後そのモーダルの保存も削除も
完全に無反応になっていた。→ 共通の `ff-confirm-dialog` に統一（カテゴリ画面も同様）。

#### 原因3: `ff-toast` が削除直後に画面を覆っていた

`.toast` が `pointer-events:all` の固定要素で、表示中の 3 秒間そこのタップを吸う。
**トーストが出るのは保存・削除の成功時だけ**なので「削除したあとだけ反応しない」に直結。
`top:80px` は import されていない `ff-header` を前提にした値だった。
→ `pointer-events:none`（閉じるボタンだけ `auto`）、モバイルは上部バナー化。

#### 原因4: Shadow DOM にグローバル CSS が届いていなかった

`ff-confirm-dialog` の「削除する／キャンセル」は実測 34px、`ff-toast` の
閉じるボタンは 14px 四方。`main.css` のモバイル 44px 規約は Shadow 境界を越えない。
既存の `tap-target-size.spec.js` も `document.querySelectorAll` で貫通しないため
**一度も検査されていなかった**。→ Shadow 内に自前のメディアクエリ、検査ヘルパーも新設。

#### 原因5: 押下フィードバックがリポジトリ全体で 0 件

`:active` / `touch-action` / `-webkit-tap-highlight-color` が 1 つも無く、
タッチには `:hover` が無いため「押しても何も起きない」体感になっていた。

#### あわせて直した既存バグ

- **`notes` が常に null で保存されていた** — フロントは `notes` を送るのに
  Controller の DTO は `Description`。`docs/openapi.yaml` の仕様（`notes`）とも不一致。
- **サーバー側に重複防御が無かった** — `SubscriptionService` に
  `EnsureServiceNameIsUniqueAsync()` を追加（409）。タイムアウト後の再送経路は
  これでしか塞げない（abort されるのはクライアント側の待ち受けだけで、
  サーバーの INSERT は完了していることがある）。
- **`.form__input` / `.form__select` / `.form__textarea` にベーススタイルが皆無**
  だった（iPhone で入力欄が本文幅の 6 割しかなかった原因）。
- **Chart.js のフォールバックに到達できなかった** — オフライン LAN では
  パケットが黙って捨てられて `error` イベントすら飛ばず、`await` が永久に解決しない。
  タイムアウト(5 秒)を追加。
- カテゴリ削除の失敗が `catch` で握り潰されて無言だった → toast 表示。
- `#logout-btn` が実測 36px。`a.btn` に下線が付いていた。
- 先月実績 0 のとき「前月比 +0.0%」（＝先月と同じ、という誤読）→「前月のデータがありません」。

#### スマホ向けデザイン（主要 5 画面）

- デザイントークン整備（`--font-family` / `--border-radius-lg` /
  `--color-text-secondary` は**参照されていたのに未定義**だった）、
  タイポスケール、`--tap-target: 48px`、`--safe-bottom`（ノッチ対応）
- サブスク: テーブル → `.sub-card`。**一覧行から直接削除**できるように
- 支出一覧: `.exp-card`。**1 画面 2 件 → 7 件**
- モーダル → ボトムシート（つまみ・sticky フッター・safe-area）
- FAB（サブスク／支出）、ダッシュボードの余白圧縮、フィルタバー整理

> **CSS カスケードの罠を踏んだ:** `.fab` と `.modal__footer` のモバイル指定を
> `main.css` に書いたが、ベース定義のある `components.css` が**後に読み込まれる**ため
> 後勝ちで負けた。**モバイル用オーバーライドは、ベース定義と同じファイルに置くこと。**

#### 検証インフラ（.NET SDK 無しでフロントを回す）

`tests/e2e/mock-server/server.js` が `src/frontend` を静的配信しつつ `/api/*` を
インメモリ応答する。`npm run test:e2e:mock`（`playwright.mock.config.js`）。
ユーザー単位に分離してあり、`Cache-Control` やエラー形式も本物と揃えてある。

### 進行中のタスク

- なし

### 次にやること

1. **SDK のある環境で `dotnet build && dotnet test` を実行する（最優先）**
   本ブランチは C# を変更している（`SubscriptionService.cs` にメソッド追加、
   `SubscriptionsController.cs` の DTO プロパティ名変更、テスト 5 本追加）が、
   コンテナに .NET SDK が無く**未検証**。
   `builds.dotnet.microsoft.com` はネットワークポリシーで遮断され導入もできない。
   前ブランチから積み残しの `StaticFileCacheHeaderTests.cs` も同様。
2. **ラズパイ実機へデプロイして確認**
   ```bash
   git pull && sudo ./infra/raspi/deploy.sh
   curl -s http://localhost:5212/ | grep -o 'app.js?v=[0-9]*'   # ?v=20260729
   ```
3. **iPhone 実機での確認**
   - サブスクを 3 件連続登録 → **3 件だけ**であること
   - 削除 → キャンセル → 再度削除 が効くこと
   - 一覧行の「削除」から直接消せること
   - 入力欄が全幅で表示され、フォーカス時にページがズームしないこと
4. **`login-overlay-regression.spec.js` の 1 スペックを直す**（今回スコープ外）
   「固着したローディングオーバーレイ」は `.loading-overlay` に `pointer-events` の
   指定が無いため、Playwright の actionability チェックが「遮られている」と判定して
   `tap()` できない。**変更前のツリーでも同じく失敗する**ことを `git stash` で確認済み。
   そもそも実ユーザーもオーバーレイ越しにはタップできないので、テストの前提が成立していない。

### 残課題（今回スコープ外）

- **Chart.js のローカル同梱** — タイムアウトを入れてフォールバックには確実に
  到達するようにしたが、オフライン環境でグラフ自体を出すには同梱が必要。
- **`.gitattributes` が無く改行コードが混在**（226 ファイル中 83 が CRLF）。
  今回は「既存ファイルの改行コードを維持する」に留めた。導入は影響範囲が広いので別途判断。
- `src/frontend/js/components/ff-header.js` と `ff-sidebar.js` は死にコード。
- `src/FinFlow.Api/Models/SubscriptionModels.cs` の record 群も死にコード
  （本物は Controller 末尾）。修正時に空振りしないよう注意。
- iOS Safari 実機での自動テストは未実施（環境に WebKit が無く Chromium のみ）。

## ブロッカー・懸念事項

- **C# のビルド・テストが未検証**（上記「次にやること」1）。
- **`SubscriptionRequest.Description` → `Notes` の改名は JSON 契約が変わる。**
  `docs/openapi.yaml` の仕様に合わせる修正で、リポジトリ内に `description` を
  前提にしたコードが無いことは確認済み。
- **エンティティ変更時はマイグレーションを 2 系統更新する必要がある**
  （SQL Server 用と SQLite 用）。手順は docs/DEPLOYMENT.md に記載。
- **E2Eスイート（`tests/e2e/`）は本番環境に対して実行しないこと**。
  `tests/e2e/helpers/auth.js` が `POST /api/auth/register` で実際にテストユーザーを
  都度登録するため。ラズパイ常駐インスタンスに対しても実行しないこと。
  （`npm run test:e2e:mock` はモックサーバー相手なので安全。）

## 重要な決定事項・メモ

### イベントリスナーの寿命（今回明文化・`.claude/rules/javascript/hooks.md`）

- **再描画されない要素（shell）へのリスナーは、ページ描画時に 1 回だけ貼る。
  `loadAndRender()` のような再取得関数からは絶対に呼ばない。**
- 一覧の項目は**永続コンテナへのイベント委譲**で扱う。
- **`innerHTML` で UI を退避・復元しない。** 復元後の要素はリスナーを失う。
- `btn.disabled` は多重リスナーには効かない。保険はハンドラ側のフラグで持つ。

### Shadow DOM

- グローバル CSS（タップ領域規約を含む）は**届かない**。コンポーネント内に自前で書く。
- CSS カスタムプロパティは継承されるが、`:root` に**実際に定義されているか**確認する。
- 検査ツールも `document.querySelectorAll` では貫通しない。
  `tests/e2e/helpers/shadow-dom.js` の `tapTargetIssuesDeep()` を使う。

### CSS の読み込み順

`main.css → components.css → pages.css`。同じ詳細度なら**後勝ち**。
**あるクラスのモバイル用オーバーライドは、ベース定義があるファイルと同じファイルに書く。**

### フロントエンドのキャッシュ運用（既存）

- **CSS/JS を変更したら `src/frontend/index.html` の `?v=` を必ず更新する。**
  現在 `20260729`。
- ES モジュールの import 指定子にクエリは伝播しないため、`?v=` が付く `app.js` だけが
  新版という**新旧混在**が起こりうる。モジュールを跨ぐ新 API は必ず
  オプショナル呼び出し（`?.`）で導入すること。
- **`index.html` のインライン起動ガードは必ずインラインのまま維持する。**
  `app.js` 末尾の `window.__ffBooted = true` はガードの成功判定なので消さないこと。

### 全画面オーバーレイの規約（既存）

- `position:fixed` + `inset:0` の要素は**見えていなくてもヒットテストは生きている**。
  非表示時は必ず `pointer-events:none`、表示時のみ `auto`。
  `opacity:0` だけではフォーカス可能なまま残るので `visibility:hidden` も併せる。
- `document.body` 直下に付けるオーバーレイはルート遷移時に明示的に片付ける。

### ラズパイ運用（既存）

- DB は **SQLite**、アクセスは **LAN 内 HTTP 5212 直接**
- 配置は publish 済みバイナリを `/opt/finflow`、DB は `/var/lib/finflow/finflow.db`
- 機密値は `/etc/finflow/finflow.env`
- systemd ユニットに設定を足すときは、**値に空白が入りうるなら必ずクォートする**
- 待ち受けアドレスは必ず `ASPNETCORE_URLS` で明示する

## ファイル変更の状態

```
# ブランチ: claude/subscription-duplicate-delete-ui-g2s2sh
#
# フロントエンド検証（Chromium モバイルエミュレーション / Pixel 7 と iPhone 相当）:
#   E2E 118 passed / 2 failed
#   失敗 2 件は login-overlay-regression.spec.js の同一スペック（両プロファイル）で、
#   git stash して変更前のツリーでも同じく失敗することを確認済み＝回帰ではない。
#   二重登録の回帰テストは、バグを一時的に戻すと落ちることを実際に確認済み。
#
# C#: dotnet build / dotnet test は SDK が無く未実行（次セッションの最優先事項）
```
