#!/usr/bin/env bash
# FinFlow ローカル開発環境セットアップスクリプト
# 動作確認済み OS: Ubuntu/Debian, macOS

set -euo pipefail

# --- ユーティリティ ---
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

info()    { echo -e "${GREEN}[INFO]${NC} $*"; }
warn()    { echo -e "${YELLOW}[WARN]${NC} $*"; }
error()   { echo -e "${RED}[ERROR]${NC} $*"; exit 1; }
section() { echo -e "\n${GREEN}=== $* ===${NC}"; }

OS=""
if [[ "$OSTYPE" == "linux-gnu"* ]]; then
    OS="linux"
elif [[ "$OSTYPE" == "darwin"* ]]; then
    OS="macos"
else
    error "未対応のOSです: $OSTYPE (Linux / macOS のみサポート)"
fi

# --- 1. .NET SDK 8 ---
section "1. .NET SDK 8 の確認・インストール"

if command -v dotnet &>/dev/null && dotnet --version | grep -q "^8\."; then
    info ".NET SDK 8 は導入済みです: $(dotnet --version)"
else
    warn ".NET SDK 8 が見つかりません。インストールします..."
    if [[ "$OS" == "linux" ]]; then
        # Microsoft 公式リポジトリ経由でインストール
        if command -v apt-get &>/dev/null; then
            # Ubuntu / Debian
            wget -q https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb \
                -O /tmp/packages-microsoft-prod.deb
            sudo dpkg -i /tmp/packages-microsoft-prod.deb
            rm /tmp/packages-microsoft-prod.deb
            sudo apt-get update -q
            sudo apt-get install -y dotnet-sdk-8.0
        elif command -v dnf &>/dev/null; then
            # Fedora / RHEL
            sudo dnf install -y dotnet-sdk-8.0
        else
            error "パッケージマネージャーが見つかりません。手動で .NET SDK 8 をインストールしてください:\n  https://dotnet.microsoft.com/download/dotnet/8.0"
        fi
    elif [[ "$OS" == "macos" ]]; then
        if command -v brew &>/dev/null; then
            brew install --cask dotnet-sdk
        else
            error "Homebrew が見つかりません。以下からインストールしてください:\n  https://dotnet.microsoft.com/download/dotnet/8.0"
        fi
    fi

    # 再確認
    if ! dotnet --version | grep -q "^8\."; then
        error ".NET SDK 8 のインストールに失敗しました。手動でインストールしてください:\n  https://dotnet.microsoft.com/download/dotnet/8.0"
    fi
    info ".NET SDK のインストール完了: $(dotnet --version)"
fi

# --- 2. dotnet-ef ツール ---
section "2. dotnet-ef (Entity Framework Core CLI) の確認・インストール"

if dotnet tool list --global 2>/dev/null | grep -q "dotnet-ef"; then
    EF_VER=$(dotnet tool list --global | grep dotnet-ef | awk '{print $2}')
    info "dotnet-ef は導入済みです: $EF_VER"
else
    warn "dotnet-ef が見つかりません。インストールします..."
    dotnet tool install --global dotnet-ef --version 8.0.0
    info "dotnet-ef のインストール完了"
fi

# PATH に ~/.dotnet/tools を追加（未追加の場合）
if [[ ":$PATH:" != *":$HOME/.dotnet/tools:"* ]]; then
    warn "~/.dotnet/tools を PATH に追加します。"
    warn "現在のシェルセッションで有効にするには: export PATH=\"\$PATH:\$HOME/.dotnet/tools\""
    export PATH="$PATH:$HOME/.dotnet/tools"
fi

# --- 3. Docker（任意: MailHog メールテスト用）---
section "3. Docker の確認（任意: メール通知テスト用 MailHog）"

if command -v docker &>/dev/null; then
    info "Docker は導入済みです: $(docker --version)"
    info "MailHog を起動するには: docker run -d -p 1025:1025 -p 8025:8025 mailhog/mailhog"
    info "MailHog 管理 UI: http://localhost:8025"
else
    warn "Docker は未導入です（メール通知のテストに必要な場合のみ）。"
    if [[ "$OS" == "linux" ]]; then
        warn "インストール: https://docs.docker.com/engine/install/"
    elif [[ "$OS" == "macos" ]]; then
        warn "インストール: https://docs.docker.com/desktop/mac/install/"
    fi
fi

# --- 4. リポジトリ確認 ---
section "4. FinFlow リポジトリの確認"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
if [[ ! -f "$SCRIPT_DIR/FinFlow.sln" ]]; then
    error "FinFlow.sln が見つかりません。リポジトリルートでスクリプトを実行してください。"
fi
info "リポジトリルート: $SCRIPT_DIR"
cd "$SCRIPT_DIR"

# --- 5. NuGet パッケージの復元とビルド ---
section "5. NuGet パッケージの復元・ビルド"

info "dotnet restore を実行..."
dotnet restore

info "dotnet build を実行..."
dotnet build --no-restore --configuration Debug

# --- 6. テスト実行（確認）---
section "6. テスト実行（確認）"

info "dotnet test を実行（InMemory DB 使用）..."
if dotnet test --no-build --configuration Debug --verbosity quiet; then
    info "全テスト合格"
else
    warn "一部テストが失敗しています。dotnet test で詳細を確認してください。"
fi

# --- 完了メッセージ ---
section "セットアップ完了"

echo ""
echo "  FinFlow ローカル開発環境の準備が整いました。"
echo ""
echo "  起動コマンド:"
echo "    dotnet run --project src/FinFlow.Api"
echo ""
echo "  アクセス先:"
echo "    フロントエンド: http://localhost:5000"
echo "    Swagger UI:    http://localhost:5000/swagger"
echo ""
echo "  開発環境は InMemory DB を使用するため SQL Server 不要です。"
echo "  本番/ステージング環境では SQL Server が必要です。"
echo ""
