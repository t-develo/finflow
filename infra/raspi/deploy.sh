#!/usr/bin/env bash
#
# FinFlow — ラズパイへのデプロイ/更新スクリプト
#
# ビルドしたアプリを /opt/finflow に配置し、systemd サービスを再起動する。
# 初回導入は install.sh から呼ばれる。2 回目以降はこのスクリプトを直接実行して更新する。
#
#   sudo ./infra/raspi/deploy.sh
#
set -euo pipefail

APP_DIR=/opt/finflow
SERVICE_NAME=finflow
SERVICE_USER=finflow
BUILD_DIR=$(mktemp -d)
REPO_ROOT=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)

cleanup() { rm -rf "$BUILD_DIR"; }
trap cleanup EXIT

# 起動に失敗したときは必ずログを画面に出す。
# systemctl の "failed because a fatal signal was delivered to the control process"
# だけでは原因が分からず、journal を見ないと切り分けできないため。
fail_with_logs() {
    echo "" >&2
    echo "エラー: $1" >&2
    echo "--- systemctl status ---" >&2
    systemctl status "$SERVICE_NAME" --no-pager -l 2>&1 | sed 's/^/  /' >&2 || true
    echo "--- journalctl (直近50行) ---" >&2
    journalctl -u "$SERVICE_NAME" -n 50 --no-pager 2>&1 | sed 's/^/  /' >&2 || true
    echo "" >&2
    echo "ヒント: 上のログに .NET の例外が出ていればアプリ側の設定エラーです。" >&2
    echo "  設定値の確認: systemctl show $SERVICE_NAME -p Environment" >&2
    echo "  詳しい対処  : docs/RASPBERRY_PI_DEPLOYMENT.md の「トラブルシューティング」" >&2
    exit 1
}

if [[ $EUID -ne 0 ]]; then
    echo "エラー: root で実行してください（sudo ./infra/raspi/deploy.sh）" >&2
    exit 1
fi

if ! command -v dotnet >/dev/null 2>&1; then
    echo "エラー: dotnet が見つかりません。先に install.sh を実行してください。" >&2
    exit 1
fi

echo "==> ビルド中（$REPO_ROOT）"
# ラズパイ上でビルドするため RID 指定は不要（framework-dependent の可搬ビルド）。
# CI の --runtime linux-x64 は Azure App Service 向けでラズパイでは誤り。
dotnet publish "$REPO_ROOT/src/FinFlow.Api" \
    --configuration Release \
    --output "$BUILD_DIR" \
    --nologo

# src/FinFlow.Api/wwwroot は ../frontend へのシンボリックリンク。
# 通常は publish が実体を追ってコピーするが、チェックアウト方法によっては
# リンクが壊れて空になることがあるため、取りこぼしを補う。
if [[ ! -f "$BUILD_DIR/wwwroot/index.html" ]]; then
    echo "==> フロントエンドが publish されなかったため手動コピー"
    mkdir -p "$BUILD_DIR/wwwroot"
    cp -RL "$REPO_ROOT/src/frontend/." "$BUILD_DIR/wwwroot/"
fi

if [[ ! -f "$BUILD_DIR/wwwroot/index.html" ]]; then
    echo "エラー: wwwroot/index.html が見つかりません。フロントエンドの配置を確認してください。" >&2
    exit 1
fi

echo "==> サービス停止"
systemctl stop "$SERVICE_NAME" 2>/dev/null || true
# Restart=always で失敗を繰り返した直後は start limit に達していて起動を拒否されるため、
# 失敗カウンタを消してから配置・起動する。
systemctl reset-failed "$SERVICE_NAME" 2>/dev/null || true

echo "==> $APP_DIR へ配置"
mkdir -p "$APP_DIR"
# --delete で旧バージョンの残骸を除去する（DB は /var/lib/finflow にあるので影響しない）
rsync -a --delete "$BUILD_DIR/" "$APP_DIR/"
chown -R "$SERVICE_USER:$SERVICE_USER" "$APP_DIR"

echo "==> サービス起動"
# set -e で即死すると下の fail_with_logs に到達せずログが出ないため、明示的に判定する
systemctl start "$SERVICE_NAME" || fail_with_logs "サービスの起動に失敗しました。"

echo "==> 起動確認"
for i in {1..15}; do
    if curl -fsS -o /dev/null http://localhost:5212/ 2>/dev/null; then
        echo ""
        echo "デプロイ完了。http://$(hostname -I | awk '{print $1}'):5212 で利用できます。"
        exit 0
    fi
    sleep 2
done

fail_with_logs "サービスは起動しましたが、30秒以内に http://localhost:5212/ へ応答しませんでした。"
