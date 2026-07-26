# ラズベリーパイ常駐運用ガイド

ラズパイ上で FinFlow を **systemd サービスとして常駐**させる手順。
電源投入だけでアプリが立ち上がり、`dotnet run` でコンソールを占有せず、クラッシュしても自動復帰する。

## 目次

1. [構成の概要](#構成の概要)
2. [前提](#前提)
3. [初回セットアップ](#初回セットアップ)
4. [アプリの更新](#アプリの更新)
5. [運用コマンド](#運用コマンド)
6. [設定値の変更](#設定値の変更)
7. [バックアップ](#バックアップ)
8. [トラブルシューティング](#トラブルシューティング)

---

## 構成の概要

| 項目 | 値 |
|------|-----|
| アクセス先 | `http://<ラズパイのIP>:5212`（家庭内 LAN・HTTP 直接） |
| 実行方式 | systemd サービス `finflow`（`Type=notify`, `Restart=always`） |
| 配置先 | `/opt/finflow`（publish 済みバイナリ） |
| データベース | **SQLite** — `/var/lib/finflow/finflow.db` |
| 機密設定 | `/etc/finflow/finflow.env`（`Jwt__Key` など） |
| 実行ユーザー | `finflow`（ログイン不可のシステムユーザー） |
| ログ | journald（`journalctl -u finflow`） |

### なぜ SQLite なのか

`ASPNETCORE_ENVIRONMENT` による既定の DB は以下のとおり:

- `Development` → InMemory（**再起動でデータが全消え**）
- それ以外 → SQL Server（**ARM ビルドが存在せずラズパイでは起動できない**）

そのためラズパイでは `Database__Provider=Sqlite` を指定して SQLite を使う。
Azure へのデプロイは従来どおり SQL Server のままで、設定を変えなければ挙動は変わらない。

> **金額の扱いについて**
> SQLite には `decimal` 型が無く、`decimal(18,2)` 列は NUMERIC affinity となり小数を含む金額が
> REAL（浮動小数点）で保存される。これは丸め誤差の原因になるため
> （例: `99999999999999.99` → `99999999999999.98`）、`FinFlowDbContext` は SQLite の場合のみ
> 金額を最小単位（1/100）の整数に変換して保存している。C# 側のプロパティは `decimal` のまま。

---

## 前提

- Raspberry Pi OS（64bit 推奨）、Debian ベース
- インターネット接続（初回の .NET 導入とパッケージ復元に必要）
- リポジトリをラズパイ上にクローン済み

```bash
git clone https://github.com/t-develo/finflow.git
cd finflow
```

> .NET SDK が未導入でも `install.sh` が公式の `dotnet-install.sh` で導入する。
> リポジトリ直下の `setup-local.sh` は **Ubuntu 向け apt フィード**を使うため
> Raspberry Pi OS では利用できない。

---

## 初回セットアップ

```bash
sudo ./infra/raspi/install.sh
```

このスクリプトが行うこと:

1. .NET 10 の導入（未導入の場合のみ）
2. サービス用ユーザー `finflow` の作成
3. `/etc/finflow/finflow.env` を生成し、**JWT 署名キーを自動生成**
   （Production では既定キーのままだとアプリが起動時に例外を投げる仕様のため）
4. `finflow.service` を `/etc/systemd/system/` に配置し **自動起動を有効化**
5. `deploy.sh` を呼んでビルド・配置・起動

完了後、`http://<ラズパイのIP>:5212` をブラウザで開いてユーザー登録すれば利用開始できる。

---

## アプリの更新

コードを更新したあとは deploy スクリプトだけでよい（再度 install.sh を実行する必要はない）。

```bash
git pull
sudo ./infra/raspi/deploy.sh
```

ビルド → サービス停止 → `/opt/finflow` へ配置 → 起動 → 疎通確認、までを自動で行う。
DB は `/var/lib/finflow/` にあるため更新の影響を受けない。
EF Core のマイグレーションは**アプリ起動時に自動適用**される。

---

## 運用コマンド

```bash
# 状態確認
systemctl status finflow

# ログ（追尾 / 直近50行）
journalctl -u finflow -f
journalctl -u finflow -n 50 --no-pager

# 再起動・停止・開始
sudo systemctl restart finflow
sudo systemctl stop finflow
sudo systemctl start finflow

# 自動起動の有効/無効
systemctl is-enabled finflow
sudo systemctl disable finflow
sudo systemctl enable finflow
```

---

## 設定値の変更

ポートや環境変数は `/etc/systemd/system/finflow.service` に、
機密値は `/etc/finflow/finflow.env` に記述されている。

```bash
# 例: ポートを変更する
sudo systemctl edit --full finflow      # ASPNETCORE_URLS を編集
sudo systemctl daemon-reload
sudo systemctl restart finflow

# 例: SMTP（メール通知）を設定する
sudo nano /etc/finflow/finflow.env
sudo systemctl restart finflow
```

> `Jwt__Key` を変更すると発行済みのログイントークンが全て無効になり、再ログインが必要になる。

---

## バックアップ

家計データは SQLite ファイル 1 つに収まっている。

```bash
# 安全なバックアップ（稼働中でも可）
sudo sqlite3 /var/lib/finflow/finflow.db ".backup '/home/pi/finflow-$(date +%Y%m%d).db'"

# sqlite3 が無い場合はサービスを止めてコピー
sudo systemctl stop finflow
sudo cp /var/lib/finflow/finflow.db /home/pi/finflow-backup.db
sudo systemctl start finflow
```

復元は逆にファイルを戻して `sudo systemctl restart finflow`。

---

## トラブルシューティング

| 症状 | 確認すること |
|------|------------|
| サービスが起動しない | `journalctl -u finflow -n 50 --no-pager` で例外を確認 |
| `fatal signal was delivered to the control process` | アプリの未処理例外。[下の節](#fatal-signal-was-delivered-to-the-control-process-と出て起動しない)を参照 |
| `Jwt:Key must be set...` で落ちる | `/etc/finflow/finflow.env` に `Jwt__Key` があるか確認 |
| 他の端末からアクセスできない | `ASPNETCORE_URLS` が `0.0.0.0` になっているか、ファイアウォールで 5212 が開いているか |
| ポートが既に使用中 | `sudo ss -lptn 'sport = :5212'` で使用プロセスを確認 |
| データが消えた | `Database__Provider` が `Sqlite` か確認（`InMemory` だと再起動で消える） |
| DB に書き込めない | `/var/lib/finflow` の所有者が `finflow` か確認（`ls -la /var/lib/finflow`） |
| 画面が真っ白 | `/opt/finflow/wwwroot/index.html` があるか確認。無ければ `deploy.sh` を再実行 |

### `fatal signal was delivered to the control process` と出て起動しない

```
Job for finflow.service failed because a fatal signal was delivered to the control process.
```

**まずこのメッセージの意味を押さえる。** systemd は失敗理由ごとに文言を出し分けており、
これは「終了コードではなく**シグナルでプロセスが死んだ**」ことを示す
（`the control process exited with error code` / `a timeout was exceeded` とは別の状態）。

.NET は Linux 上で**未処理例外を `abort()`（SIGABRT）で終了する**ため、
実際にはほぼ「**アプリが起動時に例外を投げた**」と読んでよい。systemd 側の設定ではなく、
アプリのログに答えがある。

```bash
journalctl -u finflow -n 100 --no-pager
```

出てくる例外ごとの対処:

| ログに出る内容 | 原因 | 対処 |
|---|---|---|
| `Format of the initialization string does not conform...` / `SqliteException` | `Environment=` のクォート漏れで接続文字列が壊れている（後述） | `systemctl show finflow -p Environment` で実際の値を確認 |
| `Jwt:Key must be set to a strong secret value in production` | `/etc/finflow/finflow.env` が無い・読めない・`Jwt__Key` が空 | `sudo ./infra/raspi/install.sh` を再実行するか、手動で `Jwt__Key` を設定して再起動 |
| `Unknown Database:Provider '...'` | `Database__Provider` の値が不正 | `Sqlite` を指定する |
| `unable to open database file` | `/var/lib/finflow` の権限 | `ls -ld /var/lib/finflow` が `finflow finflow` かつ `drwx------` か確認 |
| ログに例外が無く `Killed` のみ / `dmesg` に `oom-kill` | メモリ不足（RAM 1GB 以下の機種） | swap を増やす（`sudo dphys-swapfile swapoff/setup/swapon`） |

#### `Environment=` のクォートに注意

systemd の `Environment=` は**空白区切りの代入リスト**として解釈される。
値に空白を含む場合はダブルクォートで囲まないと途中で分割される。

```ini
# NG: ConnectionStrings__DefaultConnection=Data と Source=/var/... の「2変数」になる
Environment=ConnectionStrings__DefaultConnection=Data Source=/var/lib/finflow/finflow.db

# OK
Environment="ConnectionStrings__DefaultConnection=Data Source=/var/lib/finflow/finflow.db"
```

分割後の `Source=/var/...` も文法上は正しい代入なので**警告が一切出ない**。
`systemd-analyze verify` でも検出できないため、必ず解釈結果を直接見て確認する。

```bash
systemctl show finflow -p Environment
# 期待: ...ConnectionStrings__DefaultConnection=Data Source=/var/lib/finflow/finflow.db...
# 異常: ConnectionStrings__DefaultConnection=Data と Source=... に割れている
```

#### 起動失敗を繰り返したあとに `start-limit` で拒否される場合

`Restart=always` のため失敗を繰り返すと start limit に達し、
修正後も起動を拒否されることがある。失敗カウンタを消してから起動する。

```bash
sudo systemctl reset-failed finflow
sudo systemctl start finflow
```

### 起動状態の一括確認

```bash
systemctl is-enabled finflow      # enabled であること（＝自動起動する）
systemctl is-active finflow       # active であること
curl -fsS http://localhost:5212/ | head -3
```

### 自動復帰の確認

```bash
# プロセスを強制終了しても 10 秒程度で自動復帰する
sudo systemctl kill -s SIGKILL finflow
sleep 15 && systemctl is-active finflow    # active に戻る
```

---

## 関連ドキュメント

- [docs/DEPLOYMENT.md](DEPLOYMENT.md) — 一般的な Linux / オンプレ向けデプロイ手順
- [docs/AZURE_DEPLOYMENT.md](AZURE_DEPLOYMENT.md) — Azure App Service へのデプロイ
- [docs/SETUP.md](SETUP.md) — ローカル開発環境のセットアップ
