#!/usr/bin/env bash
# =============================================================================
# FinFlow Azure デプロイスクリプト
# 使い方: ./infra/scripts/deploy.sh <dev|prod>
#
# 以下を順番に実行します:
#   1. .NET アプリケーションのビルド & パブリッシュ
#   2. ARM テンプレートによるインフラデプロイ (冪等)
#   3. App Service へのアプリパッケージデプロイ
# =============================================================================

set -euo pipefail

# ----------------------------------------------------------------------------
# 色付き出力
# ----------------------------------------------------------------------------
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
BOLD='\033[1m'
NC='\033[0m'

info()    { echo -e "${CYAN}[INFO]${NC} $*"; }
success() { echo -e "${GREEN}[OK]${NC} $*"; }
warn()    { echo -e "${YELLOW}[WARN]${NC} $*"; }
error()   { echo -e "${RED}[ERROR]${NC} $*" >&2; exit 1; }
header()  { echo -e "\n${BOLD}${CYAN}=== $* ===${NC}\n"; }

# ----------------------------------------------------------------------------
# 引数チェック
# ----------------------------------------------------------------------------
ENV="${1:-}"
if [[ -z "${ENV}" ]]; then
  echo "使い方: $0 <dev|prod>"
  echo ""
  echo "例:"
  echo "  $0 dev   # 開発環境にデプロイ"
  echo "  $0 prod  # 本番環境にデプロイ"
  exit 1
fi

if [[ "${ENV}" != "dev" && "${ENV}" != "prod" ]]; then
  error "環境は 'dev' または 'prod' を指定してください"
fi

# ----------------------------------------------------------------------------
# スクリプトのルートディレクトリを決定
# ----------------------------------------------------------------------------
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
INFRA_DIR="${REPO_ROOT}/infra"

# ----------------------------------------------------------------------------
# 環境変数 / .env ファイルの読み込み
# ----------------------------------------------------------------------------
header "設定読み込み (環境: ${ENV})"

ENV_FILE="${SCRIPT_DIR}/.env.setup"
if [[ -f "${ENV_FILE}" ]]; then
  info ".env.setup ファイルを読み込みます: ${ENV_FILE}"
  # shellcheck source=/dev/null
  source "${ENV_FILE}"
fi

# 環境に応じた変数を設定
if [[ "${ENV}" == "dev" ]]; then
  RESOURCE_GROUP="${DEV_RESOURCE_GROUP:-finflow-dev-rg}"
  JWT_KEY="${DEV_JWT_KEY:-}"
  SQL_ADMIN_PASSWORD="${DEV_SQL_ADMIN_PASSWORD:-}"
else
  RESOURCE_GROUP="${PROD_RESOURCE_GROUP:-finflow-prod-rg}"
  JWT_KEY="${PROD_JWT_KEY:-}"
  SQL_ADMIN_PASSWORD="${PROD_SQL_ADMIN_PASSWORD:-}"
fi

# 必須変数チェック
for VAR in RESOURCE_GROUP JWT_KEY SQL_ADMIN_PASSWORD; do
  if [[ -z "${!VAR:-}" ]]; then
    error "${VAR} が設定されていません。\n.env.setup ファイルを確認するか、環境変数を設定してください。\n例: ${VAR}='...' $0 ${ENV}"
  fi
done

SQL_ADMIN_LOGIN="${SQL_ADMIN_LOGIN:-finflowadmin}"
SMTP_HOST="${SMTP_HOST:-}"
SMTP_PORT="${SMTP_PORT:-587}"
SMTP_ENABLE_SSL="${SMTP_ENABLE_SSL:-true}"
SMTP_USERNAME="${SMTP_USERNAME:-}"
SMTP_PASSWORD="${SMTP_PASSWORD:-}"
SMTP_FROM_ADDRESS="${SMTP_FROM_ADDRESS:-}"
SMTP_FROM_NAME="${SMTP_FROM_NAME:-FinFlow}"

success "設定の読み込み完了"
info "リソースグループ: ${RESOURCE_GROUP}"

# ----------------------------------------------------------------------------
# 前提条件チェック
# ----------------------------------------------------------------------------
header "前提条件チェック"

for cmd in az dotnet zip; do
  if command -v "$cmd" &>/dev/null; then
    success "$cmd が見つかりました"
  else
    error "$cmd がインストールされていません"
  fi
done

if ! az account show &>/dev/null; then
  error "Azure にログインしていません。先に 'az login' を実行してください"
fi

CURRENT_ACCOUNT=$(az account show --query "{name:name, id:id}" -o json)
info "使用中のアカウント: $(echo "${CURRENT_ACCOUNT}" | jq -r '.name') ($(echo "${CURRENT_ACCOUNT}" | jq -r '.id'))"

# ----------------------------------------------------------------------------
# .NET アプリケーションのビルド & パブリッシュ
# ----------------------------------------------------------------------------
header ".NET アプリケーション ビルド & パブリッシュ"

PUBLISH_DIR="${REPO_ROOT}/publish"
rm -rf "${PUBLISH_DIR}"

info "dotnet publish を実行中..."
dotnet publish "${REPO_ROOT}/src/FinFlow.Api" \
  --configuration Release \
  --output "${PUBLISH_DIR}" \
  --runtime linux-x64 \
  --self-contained false \
  --nologo

success "パブリッシュ完了: ${PUBLISH_DIR}"

# フロントエンドを wwwroot にコピー
WWWROOT="${PUBLISH_DIR}/wwwroot"
mkdir -p "${WWWROOT}"
info "フロントエンドをコピー中..."
cp -r "${REPO_ROOT}/src/frontend/." "${WWWROOT}/"
success "フロントエンドコピー完了"

# ZIP アーカイブ作成
ZIP_FILE="${REPO_ROOT}/finflow-${ENV}.zip"
info "ZIP アーカイブを作成中: ${ZIP_FILE}"
(cd "${PUBLISH_DIR}" && zip -r "${ZIP_FILE}" . -q)
success "ZIP アーカイブ作成完了: $(du -sh "${ZIP_FILE}" | cut -f1)"

# ----------------------------------------------------------------------------
# ARM テンプレートデプロイ (インフラ構築)
# ----------------------------------------------------------------------------
header "ARM テンプレートデプロイ (インフラ構築)"

TEMPLATE_FILE="${INFRA_DIR}/arm/main.json"
PARAMS_FILE="${INFRA_DIR}/arm/parameters/${ENV}.parameters.json"

info "ARM テンプレートをデプロイ中..."
info "  テンプレート: ${TEMPLATE_FILE}"
info "  パラメータ: ${PARAMS_FILE}"
info "  リソースグループ: ${RESOURCE_GROUP}"

DEPLOYMENT_OUTPUT=$(az deployment group create \
  --resource-group "${RESOURCE_GROUP}" \
  --template-file "${TEMPLATE_FILE}" \
  --parameters "@${PARAMS_FILE}" \
  --parameters \
    sqlAdminPassword="${SQL_ADMIN_PASSWORD}" \
    jwtKey="${JWT_KEY}" \
    smtpHost="${SMTP_HOST}" \
    smtpPort="${SMTP_PORT}" \
    smtpEnableSsl="${SMTP_ENABLE_SSL}" \
    smtpUsername="${SMTP_USERNAME}" \
    smtpPassword="${SMTP_PASSWORD}" \
    smtpFromAddress="${SMTP_FROM_ADDRESS}" \
    smtpFromName="${SMTP_FROM_NAME}" \
  --output json)

WEB_APP_NAME=$(echo "${DEPLOYMENT_OUTPUT}" | jq -r '.properties.outputs.webAppName.value')
WEB_APP_URL=$(echo "${DEPLOYMENT_OUTPUT}" | jq -r '.properties.outputs.webAppUrl.value')

success "ARM デプロイ完了"
info "Web App 名: ${WEB_APP_NAME}"
info "Web App URL: ${WEB_APP_URL}"

# ----------------------------------------------------------------------------
# App Service へのアプリデプロイ
# ----------------------------------------------------------------------------
header "App Service へのアプリデプロイ"

info "ZIP パッケージをデプロイ中..."
az webapp deploy \
  --resource-group "${RESOURCE_GROUP}" \
  --name "${WEB_APP_NAME}" \
  --src-path "${ZIP_FILE}" \
  --type zip \
  --output none

success "アプリデプロイ完了"

# ----------------------------------------------------------------------------
# 後片付け & 結果表示
# ----------------------------------------------------------------------------
rm -f "${ZIP_FILE}"
rm -rf "${PUBLISH_DIR}"

header "デプロイ完了"

echo -e "${GREEN}${BOLD}✓ ${ENV} 環境へのデプロイが完了しました！${NC}"
echo ""
echo -e "  アクセス URL: ${BOLD}${WEB_APP_URL}${NC}"
echo ""
echo "ヘルスチェック:"
echo "  curl -X POST ${WEB_APP_URL}/api/auth/register \\"
echo "    -H 'Content-Type: application/json' \\"
echo "    -d '{\"email\":\"test@example.com\",\"password\":\"Test1234!\",\"username\":\"testuser\"}'"
echo ""

if [[ "${ENV}" == "dev" ]]; then
  echo -e "${YELLOW}注意 (dev 環境):${NC}"
  echo "  - F1 プランはコールドスタートが発生する場合があります (~10秒)"
  echo "  - Azure SQL Serverless は初回接続時に warm-up が必要な場合があります (~30秒)"
fi
