# FinFlow デプロイ手順書

## 目次

1. [必要環境](#必要環境)
2. [開発環境のセットアップ](#開発環境のセットアップ)
3. [本番環境のセットアップ](#本番環境のセットアップ)
4. [環境変数・設定値の管理](#環境変数設定値の管理)
5. [データベースマイグレーション](#データベースマイグレーション)
6. [ヘルスチェック](#ヘルスチェック)
7. [ロールバック手順](#ロールバック手順)

---

## 必要環境

| コンポーネント | 開発環境 | 本番環境 |
|--------------|---------|---------|
| .NET SDK | 10.0 以上 | .NET 10.0 ランタイム |
| データベース | InMemory（既定）/ SQL Server LocalDB | SQL Server 2019+ / Azure SQL / SQLite |
| SMTP サーバー | MailHog (ローカル) | SendGrid / 社内 SMTP |
| OS | Windows / macOS / Linux | Linux (推奨) / Windows Server |

---

## 開発環境のセットアップ

### 1. リポジトリのクローン

```bash
git clone <repository-url>
cd finflow
```

### 2. 依存パッケージの復元とビルド

```bash
dotnet restore
dotnet build
```

### 3. データベースの準備

SQL Server LocalDB を使用する場合は別途インストール不要（Visual Studio に同梱）。
接続文字列は `src/FinFlow.Api/appsettings.Development.json` に記載されています。

```bash
# マイグレーション適用
dotnet ef database update \
  --project src/FinFlow.Infrastructure \
  --startup-project src/FinFlow.Api
```

### 4. MailHog のセットアップ（メール通知テスト用）

```bash
# Docker を使用する場合
docker run -d -p 1025:1025 -p 8025:8025 mailhog/mailhog
```

MailHog の管理 UI: http://localhost:8025

### 5. API サーバーの起動

```bash
dotnet run --project src/FinFlow.Api
```

デフォルト: http://localhost:5212
Swagger UI: http://localhost:5212/swagger
フロントエンド: http://localhost:5212/index.html

### 6. フロントエンドの確認

フロントエンドはビルド不要のバニラ JS です。API サーバーが静的ファイルを配信します。
ブラウザで `http://localhost:5212` を開いてください。

---

## 本番環境のセットアップ

### 1. アプリケーションの発行

```bash
dotnet publish src/FinFlow.Api \
  --configuration Release \
  --output ./publish
```

### 2. 環境変数の設定

**重要:** 本番環境では以下の環境変数を必ず設定してください。
`appsettings.json` の値はデフォルト値であり、本番では上書きが必須です。

```bash
# JWT 秘密鍵（必須・32文字以上のランダム文字列）
export Jwt__Key="<強力なランダム文字列（例: openssl rand -base64 48 で生成）>"

# JWT 設定
export Jwt__Issuer="FinFlowApi"
export Jwt__Audience="FinFlowClient"
export Jwt__ExpiryHours="24"

# データベースプロバイダ（省略時: Development=InMemory / それ以外=SqlServer）
# InMemory | SqlServer | Sqlite のいずれか
export Database__Provider="SqlServer"

# データベース接続文字列
export ConnectionStrings__DefaultConnection="Server=<host>;Database=FinFlow;User Id=<user>;Password=<password>;"
# SQLite を使う場合の例:
# export Database__Provider="Sqlite"
# export ConnectionStrings__DefaultConnection="Data Source=/var/lib/finflow/finflow.db"

# CORS 許可オリジン（本番フロントエンドのURL）
export Cors__AllowedOrigins__0="https://your-domain.com"

# SMTP 設定
export Smtp__Host="<smtp-host>"
export Smtp__Port="587"
export Smtp__EnableSsl="true"
export Smtp__Username="<username>"
export Smtp__Password="<password>"
export Smtp__FromAddress="noreply@your-domain.com"
export Smtp__FromName="FinFlow"
```

### 3. systemd サービス設定（Linux）

`/etc/systemd/system/finflow.service` を作成:

```ini
[Unit]
Description=FinFlow API
After=network-online.target
Wants=network-online.target

[Service]
Type=notify
WorkingDirectory=/opt/finflow
ExecStart=/usr/bin/dotnet /opt/finflow/FinFlow.Api.dll
Restart=always
RestartSec=10
SyslogIdentifier=finflow
User=finflow
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://0.0.0.0:5212
# 環境変数ファイルで機密情報を管理
EnvironmentFile=/etc/finflow/finflow.env

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl daemon-reload
sudo systemctl enable finflow
sudo systemctl start finflow
```

> **注意:** `src/FinFlow.Api/Properties/launchSettings.json` は `dotnet run` 専用で publish されず、
> systemd からは読まれない。待ち受けアドレスは必ず `ASPNETCORE_URLS` で明示すること。

> **ラズベリーパイの場合**は、上記を自動化した専用スクリプトとユニットファイルが
> `infra/raspi/` にある。SQLite を使う構成込みの手順は
> [docs/RASPBERRY_PI_DEPLOYMENT.md](RASPBERRY_PI_DEPLOYMENT.md) を参照。

### 4. リバースプロキシの設定（Nginx）

```nginx
server {
    listen 80;
    server_name your-domain.com;
    return 301 https://$host$request_uri;
}

server {
    listen 443 ssl;
    server_name your-domain.com;

    ssl_certificate /etc/letsencrypt/live/your-domain.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/your-domain.com/privkey.pem;

    location / {
        proxy_pass http://localhost:5212;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
    }
}
```

### 5. ファイアウォール設定

```bash
# 443 (HTTPS) と 80 (HTTP→HTTPS リダイレクト) のみ開放
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw deny 5212/tcp  # アプリポートは直接公開しない
```

---

## 環境変数・設定値の管理

### 設定の優先順位（高い順）

1. 環境変数（`Jwt__Key` 形式、`__` がセクション区切り）
2. `appsettings.{Environment}.json`
3. `appsettings.json`

### JWT 秘密鍵の生成

```bash
# openssl を使用して安全なキーを生成
openssl rand -base64 48
```

### セキュリティ注意事項

- `appsettings.json` の `Jwt:Key` はデフォルト値であり、本番では必ず環境変数で上書きすること
- 本番環境でデフォルトキーが設定されている場合、アプリケーションは起動時にエラーを発生させます
- 接続文字列・SMTP パスワードは環境変数または Secret Manager で管理し、ソースコードにコミットしないこと

---

## データベースマイグレーション

SQL Server 用と SQLite 用でマイグレーションのアセンブリを分けている。
エンティティやモデル設定を変更した場合は**両方**を更新すること。

| プロバイダ | マイグレーションの場所 |
|-----------|---------------------|
| SQL Server（Azure） | `src/FinFlow.Infrastructure/Migrations/` |
| SQLite（ラズパイ） | `src/FinFlow.Infrastructure.Sqlite/Migrations/` |

### 新しいマイグレーションの作成

```bash
# SQL Server 用（既定）
dotnet ef migrations add <MigrationName> \
  --project src/FinFlow.Infrastructure \
  --startup-project src/FinFlow.Api

# SQLite 用（FINFLOW_MIGRATIONS_PROVIDER でプロバイダを切り替える）
FINFLOW_MIGRATIONS_PROVIDER=Sqlite dotnet ef migrations add <MigrationName> \
  --project src/FinFlow.Infrastructure.Sqlite \
  --startup-project src/FinFlow.Api
```

### マイグレーションの適用

```bash
# 開発環境
dotnet ef database update \
  --project src/FinFlow.Infrastructure \
  --startup-project src/FinFlow.Api

# 本番環境（SQL スクリプトを生成してから適用）
dotnet ef migrations script \
  --project src/FinFlow.Infrastructure \
  --startup-project src/FinFlow.Api \
  --output migration.sql
# 生成された migration.sql を DBA がレビューして適用
```

### マイグレーションの確認

```bash
dotnet ef migrations list \
  --project src/FinFlow.Infrastructure \
  --startup-project src/FinFlow.Api
```

---

## ヘルスチェック

API の動作確認:

```bash
# 認証エンドポイント疎通確認
curl -X POST https://your-domain.com/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"testpass123"}'
```

Swagger UI（開発環境のみ）:
- http://localhost:5212/swagger

---

## ロールバック手順

### アプリケーションのロールバック

```bash
# 旧バージョンの publish フォルダに切り替え
sudo systemctl stop finflow
# 旧バージョンのバイナリを /opt/finflow に復元
sudo systemctl start finflow
```

### データベースのロールバック

```bash
# 特定のマイグレーションに戻す
dotnet ef database update <TargetMigrationName> \
  --project src/FinFlow.Infrastructure \
  --startup-project src/FinFlow.Api
```

**注意:** ロールバック前に必ずデータベースのバックアップを取得してください。
