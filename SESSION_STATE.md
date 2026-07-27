# FinFlow Session State

> このファイルはiOS Claude Codeのセッション間でコンテキストを引き継ぐために使用します。
> セッション開始時に自動的に読み込まれます。セッション中に作業状況を更新してください。

## 最終更新

- **日時:** 2026-07-27
- **担当者/エージェント:** Claude Code（iPhone で真っ白画面になる問題の修正）

## 現在のスプリント・マイルストーン

- **スプリント:** Sprint 3 相当（機能は一通り実装済み、品質・回帰テスト・運用整備フェーズ）
- **フォーカス:** ラズパイ常駐運用中に実機で見つかった不具合の修正
- **ブランチ:** `claude/iphone-white-screen-issue-mqf4xp`（`main` = `ef91c38` から分岐）

## 直前の作業内容

### 完了したタスク（E: iPhone で真っ白画面 — 本ブランチ）

**前回の修正（PR #21）が原因で発生した二次不具合。** 症状が
「表示はされるがタップ不可」→「真っ白」に変わり、リロードも `?v=` も効かず、
**プライベートブラウザでは正常**という報告。

- **原因（Chromium で再現・実証済み）: 新旧モジュールの混在**
  `index.html` の `?v=` は `<script src>` / `<link href>` にしか効かず、
  `app.js` が `import` する `router.js` / `utils/*.js` には**伝播しない**。
  各モジュールは 1 ファイル 1 URL で独立したキャッシュエントリなので、
  **「?v= が付く app.js は必ず新版 / 付かない import 先は古いまま」**という混在が成立する。
  そして PR #21 で `api-client.js` に追加した `loadingManager.reset()` を、
  端末に残っていた古い `api-client.js` が持っておらず `TypeError`。
  これが `handleRoute()` の**最初の実行文**（`container.innerHTML` に触れる前）だったため、
  **全ルートが何も描画されず真っ白**になっていた。
  `window.onerror` が無かったため実機に手掛かりが一切残らなかったのが切り分けを困難にした。
  なお `Cache-Control: no-cache` も PR #21 で入ったが、ヘッダは「これから取得する応答」に
  しか付かないため、**修正を届ける手段自体がその修正で初めて有効になる**鶏と卵だった。

- **対策 1（中核）: `index.html` にインラインの起動ガードを追加**
  起動失敗（例外・リソース読み込み失敗・無言のハング）を検知し、
  `fetch(url, { cache: 'reload' })` で**1 回だけ**自己修復（URL を変えずに焼き付いた
  エントリを置換できる唯一の手段）。それでも駄目なら白画面ではなく
  **原因テキスト＋「キャッシュを消して再読み込み」ボタン**を出す。
  破棄対象 URL は DOM と `performance.getEntriesByType('resource')` から動的収集し、
  一覧をハードコードしない。`sessionStorage` でループ防止。
  **必ずインラインのまま維持すること**（外部 .js にすると、それ自体がキャッシュ事故の対象になる）。

- **対策 2（再発防止）: モジュールを跨ぐ新 API はオプショナル呼び出し**
  `router.js` を `loadingManager?.reset?.()` に。旧 `api-client.js` と混ざっても
  白画面にならず、最悪でも前回の挙動に留まる。規約として `.claude/rules/javascript/hooks.md` に明文化。

- **`app.js` 末尾で `window.__ffBooted = true`** — 起動ガードの成功判定。消さないこと。

- **回帰テスト新設** `tests/e2e/boot-guard-regression.spec.js`（6/6 パス、両モバイルプロファイル）。
  `page.route()` で「`reset()` 追加前の api-client.js」を再現する。git SHA を固定せず
  実物から当該メソッドを `delete` するため、履歴の書き換えに強い。

> **教訓:** ビルドステップの無い ES モジュール構成では、キャッシュは
> **ファイル単位でバラバラに古くなる**。「全部まとめて新しくなる」前提のコードは壊れる。
> そして**起動時の例外は無言の白画面になる**ため、`window.onerror` 相当の受け皿は必須。

### 完了したタスク（D: iPhone でログイン画面が操作できない — PR #21 でマージ済み）

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

1. **ラズパイ実機へデプロイして確認**
   ```bash
   git pull && sudo ./infra/raspi/deploy.sh
   curl -s http://localhost:5212/js/utils/api-client.js | grep -c reset  # 1 以上（新版が配信されている）
   curl -sI http://localhost:5212/js/router.js | grep -i cache-control   # no-cache であること
   curl -s http://localhost:5212/ | grep -o 'app.js?v=[0-9]*'            # ?v=20260728
   ```
2. **iPhone 実機での確認（サイトデータを消さない状態から始めること）**
   - 通常ウィンドウで開く → **自己修復が走って正常表示される**こと（白画面のままにならない）
   - ログイン → ダッシュボード → 各ページ遷移
   - 機内モードでログイン試行 → 15 秒でエラーバナーが出て、ローディング膜が消えること
   - どうしても直らない場合の手動復旧:
     設定 → Safari → 詳細 → Web サイトデータ → ラズパイの IP を左スワイプ → 削除
3. **SDK のある環境で `dotnet build` && `dotnet test` を実行する**
   コンテナに .NET SDK が無い（`builds.dotnet.microsoft.com` がネットワークポリシーで
   ブロックされ導入もできない）ため未実行。ただし**本ブランチは C# を一切変更していない**ので
   優先度は低い。前ブランチで追加された
   `tests/FinFlow.Tests/Infrastructure/StaticFileCacheHeaderTests.cs` は依然として未実行。
4. **認証が要る E2E の実行** — API サーバが必要なため本コンテナでは未実行
   （静的サーバで代用したため、ログインを伴う 10 件は失敗する。
   **変更前のツリーでも同じく失敗する**ことを確認済みで、回帰ではない）。

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

- **本ブランチは C# を一切変更していない**ため、ビルド未検証によるリスクは低い
  （SDK が無いのは変わらず。詳細は「次にやること」3）。
  フロントエンドは Chromium（モバイルエミュレーション）で実ブラウザ検証済み:
  新規回帰スペック 6/6（両モバイルプロファイル）＋ 独自ハーネス 9/9。
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
- **この制約は「古いまま」では終わらず、`?v=` が付く app.js だけが新版になる
  「新旧混在」を生む。**モジュールを跨ぐ新 API は必ずオプショナル呼び出し（`?.`）で
  導入すること。実際にこれで白画面事故が起きた（上記タスク E）。
- **`index.html` のインライン起動ガードは必ずインラインのまま維持する。**
  外部 `.js` に切り出すと、それ自体がキャッシュ事故の対象になり肝心なときに動かない。
  `app.js` 末尾の `window.__ffBooted = true` はガードの成功判定なので消さないこと。

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
# ブランチ: claude/iphone-white-screen-issue-mqf4xp
# C#: 変更なし（dotnet build / test は SDK が無く未実行だが、変更が無いため影響なし）
#
# 変更したファイル:
#   src/frontend/index.html                    起動ガード（インライン）/ <noscript> / ?v=20260728
#   src/frontend/js/router.js                  loadingManager?.reset?.()
#   src/frontend/js/app.js                     末尾に window.__ffBooted = true
#   tests/e2e/boot-guard-regression.spec.js    新規（回帰テスト）
#   .claude/rules/javascript/hooks.md          規約を明文化
#
# フロントエンド検証（Chromium モバイルエミュレーション）:
#   - boot-guard-regression: 6/6 パス（pixel7 / iphone-size 両方）
#       * 旧 api-client.js が混ざっても白画面にならず操作できる
#       * モジュール取得失敗時にエラー画面＋復旧ボタンが出る（自己修復は1回のみ）
#       * 正常系でウォッチドッグが誤発火しない
#   - 独自ハーネス: 9/9 パス（修正前コードで白画面が再現することの実証を含む）
#   - ログイン必須の既存 E2E 10 件は API 不在で失敗するが、
#     変更前のツリーでも同じく失敗することを確認済み（回帰ではない）
```
