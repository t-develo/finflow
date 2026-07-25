# FinFlow Session State

> このファイルはiOS Claude Codeのセッション間でコンテキストを引き継ぐために使用します。
> セッション開始時に自動的に読み込まれます。セッション中に作業状況を更新してください。

## 最終更新

- **日時:** 2026-07-25
- **担当者/エージェント:** Claude Code（ラズパイ常駐化タスク）

## 現在のスプリント・マイルストーン

- **スプリント:** Sprint 2 完了後の運用整備
- **フォーカス:** ラズベリーパイでの常駐運用（systemd 自動起動 + SQLite 永続化）

## 直前の作業内容

### 完了したタスク

- **systemd サービス化** — `infra/raspi/finflow.service` を追加。ラズパイ起動時に自動開始、
  クラッシュ時は 10 秒後に自動復帰。`Type=notify` で実際の待受開始を待つ。
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
  HTTP のみの LAN 構成でのログ汚染を防ぐ。
- **ドキュメント** — `docs/RASPBERRY_PI_DEPLOYMENT.md` を新規作成。
  `docs/DEPLOYMENT.md` の古い記述（ポート 5000/5001、SQL Server 前提）を修正。

### 進行中のタスク

- なし

### 次にやること

- **実機での検証**（未実施 — このセッションは x64 コンテナ上のため）
  ```bash
  sudo ./infra/raspi/install.sh
  systemctl is-enabled finflow && systemctl is-active finflow
  sudo reboot   # 再起動後に自動で上がることを確認
  ```

## ブロッカー・懸念事項

- **実機未検証** — ロジックは x64 コンテナ上で publish/起動/永続化まで確認済みだが、
  `install.sh` の .NET 導入部分（`dotnet-install.sh` の ARM 向け動作）と
  systemd 実機動作はラズパイ上での確認が必要。
- **エンティティ変更時はマイグレーションを 2 系統更新する必要がある**
  （SQL Server 用と SQLite 用）。手順は docs/DEPLOYMENT.md に記載。

## 重要な決定事項・メモ

- ラズパイの DB は **SQLite** に決定（2026-07-25）。SQL Server に ARM ビルドが無く、
  InMemory では再起動でデータが消えるため。
- ラズパイのアクセスは **LAN 内 HTTP 5212 直接**（nginx リバースプロキシは使わない）。
- 配置方式は **publish 済みバイナリを /opt/finflow**（`dotnet run` 常駐はしない）。
- DB ファイルは `/var/lib/finflow/finflow.db`（systemd の `StateDirectory` が管理）。
- 機密値は `/etc/finflow/finflow.env`。`install.sh` が `Jwt__Key` を自動生成する。

## ファイル変更の状態

```
# ブランチ: claude/raspi-app-autostart-service-3nb6ne
# dotnet build: 成功（警告 0）
# dotnet test : 198/198 成功
```
