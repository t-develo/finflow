# FinFlow Session State

> このファイルはiOS Claude Codeのセッション間でコンテキストを引き継ぐために使用します。
> セッション開始時に自動的に読み込まれます。セッション中に作業状況を更新してください。

## 最終更新

- **日時:** 2026-07-26
- **担当者/エージェント:** Claude Code（ラズパイ実機で起きたサービス起動失敗の修正）

## 現在のスプリント・マイルストーン

- **スプリント:** Sprint 3 相当（機能は一通り実装済み、品質・回帰テスト・運用整備フェーズ）
- **フォーカス:**
  1. ラズベリーパイでの常駐運用（systemd 自動起動 + SQLite 永続化）← 本ブランチ
  2. モバイル操作性の修正と Playwright E2E 回帰テスト整備 ← main で完了済み
- **ブランチ:** `claude/finflow-service-startup-error-qkfzgj`（PR #19 マージ後の `main` から分岐）

## 直前の作業内容

### 完了したタスク（C: ラズパイ実機での起動失敗を修正 — 本ブランチ）

実機で `sudo ./infra/raspi/install.sh` を実行したところ、ビルド・配置は成功するが
`systemctl start finflow` が
`Job for finflow.service failed because a fatal signal was delivered to the control process.`
で失敗した。

- **根本原因: `finflow.service` の `Environment=` にクォートされていない空白があった。**
  systemd の `Environment=` は「空白区切りの代入リスト」として解釈されるため、
  `Environment=ConnectionStrings__DefaultConnection=Data Source=/var/lib/finflow/finflow.db` は
  `ConnectionStrings__DefaultConnection=Data` と `Source=/var/lib/...` の**2変数に分割**されていた。
  結果 `UseSqlite("Data")` となり `Program.cs:199` の `db.Database.Migrate()` で例外 →
  .NET は Linux で未処理例外を `abort()`（SIGABRT）で終了するため
  systemd が `result=signal` と判定し上記メッセージになっていた。
  → `Environment=` の行を一律ダブルクォートで囲んだ。
  分割後の `Source=...` も文法上正しい代入なので**警告が一切出ず**、
  `systemd-analyze verify` でも検出できない点に注意（`systemd-analyze` で実証済み）。
- **失敗時にログが出ない問題を修正** — `deploy.sh` の `systemctl start` が `set -e` 配下で
  無防備だったため、用意されていた journalctl の案内に到達せずスクリプトが中断していた。
  `fail_with_logs()` を追加し、`systemctl status` と `journalctl -n 50` を必ず表示するようにした。
- **start limit 対策** — `Restart=always` で失敗を繰り返した後は起動を拒否されるため、
  `deploy.sh` に `systemctl reset-failed` を追加。
- **ドキュメント** — `docs/RASPBERRY_PI_DEPLOYMENT.md` に
  「`fatal signal was delivered to the control process` と出て起動しない」節を追加。
  メッセージの読み方（＝アプリの未処理例外）、例外別の対処表、
  `systemctl show finflow -p Environment` による切り分け手順を記載。

> **教訓:** systemd ユニットに設定を足すときは、値に空白が入りうるなら必ずクォートする。
> 検証は `systemctl show <unit> -p Environment` で**解釈後の値**を見ること。

### 完了したタスク（A: ラズパイ常駐化 — PR #19 でマージ済み）

- **systemd サービス化** — `infra/raspi/finflow.service` を追加。ラズパイ起動時に自動開始、
  クラッシュ時は 10 秒後に自動復帰。`Type=notify` で実際の待受開始を待つ。
  あわせて `infra/raspi/install.sh`（初回セットアップ）と `deploy.sh`（更新）を追加。
- **SQLite プロバイダ対応** — `Database:Provider`（`InMemory`/`SqlServer`/`Sqlite`）を導入。
  既定値は従来どおり（Development=InMemory / それ以外=SqlServer）なので Azure は無変更。
- **金額の精度問題を修正** — SQLite では `decimal(18,2)` 列が NUMERIC affinity となり
  小数を含む金額が REAL（浮動小数点）で保存され丸め誤差が出る
  （`99999999999999.99` → `99999999999999.98`）。SQLite の場合のみ最小単位(×100)の
  `long` へ値変換して INTEGER 列に保存するようにした。C# 側は `decimal` のまま。
- **SQLite 用マイグレーション** — `src/FinFlow.Infrastructure.Sqlite` を新設し、
  SQL Server 用（Azure）とアセンブリを分けて共存させた。
- **ページング順序の修正** — `ExpenseService.ApplyFilter` が `OrderBy` 無しで `Skip/Take` して
  いたため、リレーショナル DB ではページ間で行の重複・欠落が起こりうる状態だった。
  日付降順（同日は Id 降順）で固定。
- **HTTPS リダイレクト** — `Https:RedirectEnabled`（既定 true）で無効化可能にした。
  HTTP のみの LAN 構成でのログ汚染を防ぐ。既定値は従来どおりのため他環境に影響なし。
- **ドキュメント** — `docs/RASPBERRY_PI_DEPLOYMENT.md` を新規作成。
  `docs/DEPLOYMENT.md` の古い記述（ポート 5000/5001、SQL Server 前提）を修正。

### 完了したタスク（B: モバイル操作性 — main から取り込み / PR #18）

「スマホでログイン画面のタップが一切効かない」という報告を起点に、全画面のモバイル操作性を監査し修正した。

- **根本原因の修正**: `.sidebar-overlay` がモバイル幅で `opacity:0` のまま `position:fixed; inset:0; z-index:90` の
  全画面当たり判定を持ち続け、全タップを吸っていた問題を `pointer-events: none/auto` の切り替えで解消
- `[hidden]` 属性が同詳細度の作成者スタイル（`.btn` 等）に負けて無視される問題に対し、`[hidden]{display:none!important}`
  のグローバルガードを追加（サブスク新規作成時の削除ボタン残存、認証画面のハンバーガー誤表示等を解消）
- 未定義だった約100個のBEMクラスを補完（`src/frontend/css/components.css`・`src/frontend/css/pages.css` を新設）
- `/expenses/new` と `/expenses/:id/edit` がルーター未登録で到達不能だった問題を `router.js` の `:param` パターン
  マッチ対応で解消
- `index.html` のアセット参照をルート相対パスに変更。深いURLの直接アクセス・リロード時に JS/CSS が
  読み込めなかった問題を解消
- CSV取込のドロップゾーンに `click` ハンドラを追加（従来は dragover/drop のみでタッチデバイスでは死領域だった）
- `GET /api/expenses` のレスポンス形式とフロント側の期待の不一致を修正。`SumExpensesAsync` を追加し
  `totalAmount` をAPIレスポンスに追加（ページングと独立した全件合計）
- ドロワー展開時にハンバーガーボタンが隠れ再タップで閉じられない問題を修正
- タップ領域を44px確保（モバイル時のみ）
- `ExpenseResponse` に `CategoryColor` を追加（カテゴリバッジが常にグレーだった問題）
- Playwright によるタッチエミュレーションE2Eスイートを `tests/e2e/` に追加（Chromium）
- `docs/frontend-e2e-checklist.md` に「10. モバイル操作性」節を追記

### 進行中のタスク

- なし

### 次にやること

- **ラズパイ実機で今回の修正を検証**（最優先。作業環境が x64 コンテナのため未実施）
  ```bash
  git pull
  sudo ./infra/raspi/install.sh
  systemctl show finflow -p Environment   # 接続文字列が1つの値になっていること
  systemctl is-enabled finflow && systemctl is-active finflow
  curl -fsS http://localhost:5212/ >/dev/null && echo OK
  sudo reboot   # 再起動後に自動で上がることを確認
  ```
- `src/frontend/js/components/ff-header.js` と `ff-sidebar.js` はどこからも import されていない**死にコード**
  （`index.html` の静的サイドバー実装と重複）。削除を推奨
- `src/frontend/index.html` の Chart.js CDN `<script>` の `integrity`（sha384）ハッシュが未照合。
  ネットワークのある環境で実ファイルの sha384 を再計算して照合すること。
  オフラインの tailnet／ラズパイ LAN 環境で使うなら Chart.js 自体をローカル同梱化するのが望ましい
- iOS Safari 実機での確認が未実施（環境に WebKit が無く Chromium のみ）

## ブロッカー・懸念事項

- **ラズパイ実機未検証** — publish/起動/永続化までは x64 上で確認済みだが、
  `install.sh` の .NET 導入部分（`dotnet-install.sh` の ARM 向け動作）と
  systemd 実機動作はラズパイ上での確認が必要。
- **エンティティ変更時はマイグレーションを 2 系統更新する必要がある**
  （SQL Server 用と SQLite 用）。手順は docs/DEPLOYMENT.md に記載。
- **E2Eスイート（`tests/e2e/`）は本番環境に対して実行しないこと**。
  `tests/e2e/helpers/auth.js` が `POST /api/auth/register` で実際にテストユーザーを都度登録するため。
  ラズパイ常駐インスタンスに対しても実行しないこと。

## 重要な決定事項・メモ

### ラズパイ運用

- ラズパイの DB は **SQLite** に決定（2026-07-25）。SQL Server に ARM ビルドが無く、
  InMemory では再起動でデータが消えるため。
- ラズパイのアクセスは **LAN 内 HTTP 5212 直接**（nginx リバースプロキシは使わない）。
- 配置方式は **publish 済みバイナリを /opt/finflow**（`dotnet run` 常駐はしない）。
- DB ファイルは `/var/lib/finflow/finflow.db`（systemd の `StateDirectory` が管理）。
- 機密値は `/etc/finflow/finflow.env`。`install.sh` が `Jwt__Key` を自動生成する。
- `launchSettings.json` は `dotnet run` 専用で publish されず systemd からは読まれない。
  待ち受けアドレスは必ず `ASPNETCORE_URLS` で明示すること。

### フロントエンド／モバイル

- モーダルの表示制御は `hidden` 属性 + `[hidden]{display:none!important}` グローバルガードに統一
- 44pxタップ領域の拡大は「モバイル時（`@media (max-width:768px)`）のみ」に限定
- ドロワー開閉中の背面スクロール抑止は `overflow:hidden`（`.body--drawer-open`）のみを採用
  （`position:fixed` はスクロール位置が飛ぶ副作用のため不採用）
- Playwright は WebKit 非対応環境のため全プロジェクトを Chromium ベースで構成
- `playwright.config.js` の `workers` はローカル実行時のみ 1 に固定

## ファイル変更の状態

```
# ブランチ: claude/raspi-app-autostart-service-3nb6ne（origin/main をマージ済み）
# dotnet build: 成功（警告 0）
# dotnet test : 210/210 成功
# E2E (Playwright): 未実行（別途 npm 経由で実行）
```
