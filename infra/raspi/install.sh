#!/usr/bin/env bash
#
# FinFlow — ラズパイ初回セットアップスクリプト
#
# .NET ランタイム/SDK の導入、専用ユーザー作成、機密ファイル生成、
# systemd サービスの登録（自動起動の有効化）までを行い、最後に deploy.sh を呼ぶ。
#
#   sudo ./infra/raspi/install.sh
#
# 2 回目以降のアプリ更新は deploy.sh だけでよい。
#
set -euo pipefail

SERVICE_NAME=finflow
SERVICE_USER=finflow
SECRETS_DIR=/etc/finflow
SECRETS_FILE="$SECRETS_DIR/finflow.env"
DOTNET_ROOT_DIR=/usr/share/dotnet
DOTNET_CHANNEL=10.0
REPO_ROOT=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)

if [[ $EUID -ne 0 ]]; then
    echo "エラー: root で実行してください（sudo ./infra/raspi/install.sh）" >&2
    exit 1
fi

echo "==> 必要なパッケージを確認"
apt-get update -qq
apt-get install -y -qq curl rsync openssl ca-certificates

echo "==> .NET $DOTNET_CHANNEL を確認"
if command -v dotnet >/dev/null 2>&1 && dotnet --list-sdks 2>/dev/null | grep -q "^${DOTNET_CHANNEL}"; then
    echo "    導入済み: $(dotnet --version)"
else
    # Raspberry Pi OS は Debian ベースのため、リポジトリ直下の setup-local.sh が使う
    # Ubuntu 向け apt フィードは利用できない。公式インストールスクリプトを使う。
    echo "    dotnet-install.sh で導入します"
    curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
    chmod +x /tmp/dotnet-install.sh
    /tmp/dotnet-install.sh --channel "$DOTNET_CHANNEL" --install-dir "$DOTNET_ROOT_DIR"
    ln -sf "$DOTNET_ROOT_DIR/dotnet" /usr/bin/dotnet
    rm -f /tmp/dotnet-install.sh
    echo "    導入完了: $(dotnet --version)"
fi

echo "==> サービス用ユーザー '$SERVICE_USER' を確認"
if id "$SERVICE_USER" >/dev/null 2>&1; then
    echo "    既に存在します"
else
    useradd --system --no-create-home --shell /usr/sbin/nologin "$SERVICE_USER"
    echo "    作成しました"
fi

echo "==> 機密設定ファイル $SECRETS_FILE を確認"
mkdir -p "$SECRETS_DIR"
if [[ -f "$SECRETS_FILE" ]]; then
    echo "    既に存在するため上書きしません（JWT キーを変更すると既存トークンが無効になります）"
else
    # Production では既定の JWT キーだとアプリが起動時に例外を投げる仕様のため、
    # ここで強度のあるキーを自動生成する。
    cat > "$SECRETS_FILE" <<EOF
# FinFlow の機密設定。このファイルは systemd の EnvironmentFile として読み込まれます。
# 値を変更したら: sudo systemctl restart $SERVICE_NAME

# JWT 署名キー（32文字以上）。変更すると発行済みトークンは全て無効になります。
Jwt__Key=$(openssl rand -base64 48 | tr -d '\n')

# メール通知を使う場合は SMTP 設定を記入してください（未設定でもアプリは動作します）。
#Smtp__Host=smtp.example.com
#Smtp__Port=587
#Smtp__EnableSsl=true
#Smtp__Username=
#Smtp__Password=
#Smtp__FromAddress=noreply@example.com
#Smtp__FromName=FinFlow
EOF
    echo "    JWT キーを生成しました"
fi
chown root:"$SERVICE_USER" "$SECRETS_FILE"
chmod 640 "$SECRETS_FILE"

echo "==> systemd サービスを登録"
install -m 644 "$REPO_ROOT/infra/raspi/finflow.service" "/etc/systemd/system/$SERVICE_NAME.service"
systemctl daemon-reload
# ラズパイ起動時に自動でサービスを開始する（今回の主目的）
systemctl enable "$SERVICE_NAME"
echo "    自動起動を有効化しました"

echo "==> アプリをビルドして配置"
"$REPO_ROOT/infra/raspi/deploy.sh"

cat <<EOF

============================================================
セットアップ完了
============================================================
  アクセス先 : http://$(hostname -I | awk '{print $1}'):5212
  DB ファイル: /var/lib/finflow/finflow.db
  機密設定   : $SECRETS_FILE

よく使うコマンド:
  systemctl status $SERVICE_NAME        # 状態確認
  journalctl -u $SERVICE_NAME -f        # ログ追尾
  systemctl restart $SERVICE_NAME       # 再起動
  sudo ./infra/raspi/deploy.sh          # アプリ更新
============================================================
EOF
