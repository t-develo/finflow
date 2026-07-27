# FinFlow Session State

> このファイルはiOS Claude Codeのセッション間でコンテキストを引き継ぐために使用します。
> セッション開始時に自動的に読み込まれます。セッション中に作業状況を更新してください。

## 最終更新

- **日時:** 2026-07-27
- **担当者/エージェント:** Claude Code（iPhone でログイン画面が操作できない問題の修正）

## 現在のスプリント・マイルストーン

- **スプリント:** Sprint 3 相当（機能は一通り実装済み、品質・回帰テスト・運用整備フェーズ）
- **フォーカス:** ラズパイ常駐運用中に実機で見つかった不具合の修正
- **ブランチ:** `claude/login-screen-input-issue-mc3lj5`（`main` = `67d75b2` から分岐）

## 直前の作業内容

### 完了したタスク（D: iPhone でログイン画面が操作できない — 本ブランチ）

systemd 常駐は成功しているが、**iPhone / iOS Safari で `/login` を開くと
「見た目は正常なのにタップが一切効かない」**という報告。
PR #18 (`3eddd1f`) の `.sidebar-overlay` の `pointer-events` 修正は HEAD に入っており、
**ソース上 `/login` を静的に覆う要素は存在しない**ことを確認したうえで、
「修正が端末に届いていない経路」と「修正が塞ぎ切れていない経路」の両方を潰した。

- **本命: 静的ファイルにキャッシュ制御が無かった**
  `app.UseStaticFiles()` を素で呼ぶと ETag / Last-Modified は付くが `Cache-Control` が付かない。
  この場合ブラウザはヒューリスティックキャッシュに落ち、**再検証せずに修正前の CSS を
  使い続ける**（iOS Safari で特に顕著）。「見た目は普通だがタップが効かない」＝
  修正前の透明シャッターがそのまま生きている状態と完全に一致する。
  → `Program.cs` で `Cache-Control: no-cache, must-revalidate` を全静的ファイルに付与。
  **`MapFallbackToFile` は内部で独立した StaticFileMiddleware を実行する**ため、
  同じ `StaticFileOptions` を渡さないと index.html にだけヘッダーが付かない点に注意。
  → あわせて `index.html` の CSS/JS 参照に `?v=20260727` を付与。
  サーバー側ヘッダーは「これから取得するもの」しか直せないが、
  URL を変えるこの方法は**すでに端末に焼き付いたキャッシュ**にも即座に効く。

- **`.loading-overlay` の固着** — `position:fixed; inset:0; z-index:500` の全画面要素を
  `api-client.js` が `document.body` 直下に付け、除去は `finally` の `hide()` のみ。
  **fetch にタイムアウトが無かった**ため、LAN 断や Pi のハングで Promise が解決しないと
  overlay が永久に残り、画面全体が操作不能になる。`#page-container` の外なのでルート遷移でも消えない。
  → `AbortController` によるタイムアウト（通常 15 秒 / アップロード 120 秒）、
  `loadingManager.reset()` の追加、ルート遷移時と 401 リダイレクト時の掃除を実装。

- **ドロワー状態がルート遷移でリセットされなかった** — ドロワーは
  「サイドバー内の `[data-navigo]` クリック」でしか閉じていなかったため、
  ログアウトボタン（`#logout-btn` は `[data-navigo]` ではない）・401 リダイレクト・
  `popstate` のいずれかで `/login` に来ると `.sidebar-overlay--visible`
  （`pointer-events:auto` の全画面要素）が残り、遷移先の全タップを吸い込んでいた。
  → `router.onRouteChange` で必ず `closeSidebar()` を呼ぶようにした。

- **Chart.js の同期 CDN `<script>` を削除** — `integrity` が**空文字列の SHA-384**
  （プレースホルダ）で永久にロードされないうえ、`defer`/`async` 無しだったため、
  ネットに出られない LAN 内 Pi では DNS/TCP タイムアウトまで `<body>` の解析が止まり、
  その間ページ全体がタップに反応しない。ダッシュボード表示時に動的ロードする方式に変更。
  既存の `renderCategoryListFallback` がそのままフォールバックとして機能する。

- **`.modal` にも `pointer-events` 規律を横展開** — `hidden` 属性だけが最後の砦という
  構造は、旧 `.sidebar-overlay` と同型で壊れるため。

> **教訓:** 「修正したのに実機で直らない」ときは、コードを疑う前に
> **プライベートウィンドウで開いて切り分ける**こと。
> ビルドステップの無いフロントエンドでは、キャッシュ破棄は自動化されない。

### 完了したタスク（C: ラズパイ実機の起動失敗 — PR #20 でマージ済み）

- `finflow.service` の `Environment=` に**クォートされていない空白**があり、
  接続文字列が 2 変数に分割されて `UseSqlite("Data")` となり `Migrate()` で例外 →
  .NET は Linux で未処理例外を `abort()` (SIGABRT) で終了するため
  systemd が `fatal signal was delivered to the control process` と報告していた。
  → `Environment=` を一律ダブルクォートで囲んだ。
  分割後も文法上正しい代入のため**警告が一切出ず** `systemd-analyze verify` でも検出できない。
- `deploy.sh` に `fail_with_logs()` と `systemctl reset-failed` を追加。

### 完了したタスク（A/B: ラズパイ常駐化 / モバイル操作性 — PR #19, #18 でマージ済み）

詳細は各 PR を参照。要点のみ:
- systemd サービス化 + SQLite 永続化（金額は最小単位の `long` で INTEGER 列に保存）
- `.sidebar-overlay` の `pointer-events` 修正、`[hidden]{display:none!important}` ガード
- 未定義だった約100個の BEM クラスを補完、Playwright E2E スイートを新設

### 進行中のタスク

- なし

### 次にやること

1. **SDK のある環境で `dotnet build` && `dotnet test` を実行する（最優先）**
   本セッションのコンテナには .NET SDK が無く（`builds.dotnet.microsoft.com` が
   ネットワークポリシーでブロックされ導入もできない）、**C# のビルド確認ができていない。**
   新規追加した `tests/FinFlow.Tests/Infrastructure/StaticFileCacheHeaderTests.cs` は
   一度も実行していない。`WebApplicationFactory` から
   シンボリックリンク（`wwwroot` → `../frontend`）越しに静的ファイルを解決できるかに
   依存しており、解決できない場合は 404 で落ちる。その場合はテスト側を修正する。
2. **`npx playwright test` の実行**（`tests/e2e/login-overlay-regression.spec.js` も未実行）
3. **ラズパイ実機での確認**
   ```bash
   git pull && sudo ./infra/raspi/deploy.sh
   curl -sI http://localhost:5212/css/main.css | grep -i cache-control  # no-cache であること
   curl -s http://localhost:5212/ | grep -o 'css/main.css?v=[0-9]*'     # ?v= が付いていること
   ```
4. **iPhone 実機での確認**
   - まず**プライベートウィンドウ**で開く → 直っていれば原因はキャッシュで確定
   - 通常ウィンドウで再読み込み → 入力できること
   - ドロワーを開いた状態でログアウト → `/login` でメール欄をタップできること
   - 機内モードでログイン試行 → 15 秒でエラーバナーが出て、ローディング膜が消えること

### 残課題（今回スコープ外）

- **Chart.js のローカル同梱** — 現在も CDN から動的ロードするため、
  オフラインの tailnet／ラズパイ LAN 環境ではグラフが表示されず
  リスト表示にフォールバックする。オフライン運用を前提にするなら同梱すべき。
  なお削除した `integrity` は空文字列のハッシュ（＝無効）だったので、
  再度付けるなら実ファイルの sha384 を計算して照合すること。
- `src/frontend/js/components/ff-header.js` と `ff-sidebar.js` はどこからも
  import されていない**死にコード**（`index.html` の静的サイドバー実装と重複）。削除を推奨。
- iOS Safari 実機での自動テストは未実施（環境に WebKit が無く Chromium のみ）。

## ブロッカー・懸念事項

- **本ブランチは C# のビルド検証ができていない**（上記「次にやること」1 を参照）。
  フロントエンドは Chromium（モバイルエミュレーション）で 12/12 の実ブラウザ検証済み。
- **エンティティ変更時はマイグレーションを 2 系統更新する必要がある**
  （SQL Server 用と SQLite 用）。手順は docs/DEPLOYMENT.md に記載。
- **E2Eスイート（`tests/e2e/`）は本番環境に対して実行しないこと**。
  `tests/e2e/helpers/auth.js` が `POST /api/auth/register` で実際にテストユーザーを都度登録するため。
  ラズパイ常駐インスタンスに対しても実行しないこと。

## 重要な決定事項・メモ

### フロントエンドのキャッシュ運用（今回追加）

- **CSS/JS を変更したら `src/frontend/index.html` の `?v=` を必ず更新する。**
  ビルドステップを持たない方針のため手動管理。
  チェックリストは `.claude/rules/javascript/hooks.md` に追記済み。
- 制約: ES モジュールの import 指定子にクエリは伝播しないため、
  `app.js` が import する `router.js` / `utils/*.js` / `pages/*.js` には `?v=` が付かない。
  それらはサーバー側の `Cache-Control: no-cache` に依存する。

### 全画面オーバーレイの規約（今回明文化）

- `position:fixed` + `inset:0` の要素は**見えていなくてもヒットテストは生きている**。
  非表示時は必ず `pointer-events:none`、表示時のみ `auto`。
- `document.body` 直下に付けるオーバーレイは `#page-container` のクリアでは消えないため、
  ルート遷移時に明示的に片付ける（`loadingManager.reset()` / `closeSidebar()`）。

### ラズパイ運用

- DB は **SQLite**（SQL Server に ARM ビルドが無く、InMemory では再起動で消えるため）
- アクセスは **LAN 内 HTTP 5212 直接**（nginx リバースプロキシは使わない）
- 配置は **publish 済みバイナリを /opt/finflow**、DB は `/var/lib/finflow/finflow.db`
- 機密値は `/etc/finflow/finflow.env`（`install.sh` が `Jwt__Key` を自動生成）
- systemd ユニットに設定を足すときは、**値に空白が入りうるなら必ずクォートする**。
  検証は `systemctl show <unit> -p Environment` で解釈後の値を見ること。
- `launchSettings.json` は publish されず systemd からは読まれない。
  待ち受けアドレスは必ず `ASPNETCORE_URLS` で明示すること。

### フロントエンド／モバイル

- モーダルの表示制御は `hidden` 属性 + `[hidden]{display:none!important}` グローバルガード
- 44px タップ領域の拡大は「モバイル時（`@media (max-width:768px)`）のみ」に限定
- ドロワー開閉中の背面スクロール抑止は `overflow:hidden`（`.body--drawer-open`）のみ
  （`position:fixed` はスクロール位置が飛ぶ副作用のため不採用）
- Playwright は WebKit 非対応環境のため全プロジェクトを Chromium ベースで構成

## ファイル変更の状態

```
# ブランチ: claude/login-screen-input-issue-mc3lj5
# dotnet build: 未実行（コンテナに .NET SDK が無い）
# dotnet test : 未実行（同上）
# フロントエンド: Chromium モバイルエミュレーションで 12/12 パス
#   - ログイン画面のタップ／ヒットテスト
#   - ドロワー展開中のログアウト後も overlay が残らない
#   - 固着した .loading-overlay がルート遷移で除去される
#   - Chart.js 取得失敗でもダッシュボードが操作不能にならない
```
