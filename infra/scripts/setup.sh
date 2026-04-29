#!/usr/bin/env bash
# =============================================================================
# FinFlow Azure 初回セットアップスクリプト
# 使い方: ./infra/scripts/setup.sh
#
# このスクリプトは初回1回だけ実行してください。
# - Azure リソースグループの作成
# - GitHub Actions 用サービスプリンシパルの作成
# - 設定すべき GitHub Secrets の一覧出力
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
# 前提条件チェック
# ----------------------------------------------------------------------------
header "前提条件チェック"

for cmd in az jq openssl; do
  if command -v "$cmd" &>/dev/null; then
    success "$cmd が見つかりました"
  else
    error "$cmd がインストールされていません。インストール後に再実行してください。\n  az:      https://learn.microsoft.com/cli/azure/install-azure-cli\n  jq:      https://jqlang.github.io/jq/download/\n  openssl: OS 標準でインストール済みのはずです"
  fi
done

# ----------------------------------------------------------------------------
# Azure ログイン
# ----------------------------------------------------------------------------
header "Azure ログイン"

if az account show &>/dev/null; then
  CURRENT_USER=$(az account show --query user.name -o tsv)
  info "既にログイン済み: ${CURRENT_USER}"
  read -rp "このアカウントで続行しますか? [Y/n]: " CONTINUE
  if [[ "${CONTINUE}" =~ ^[Nn] ]]; then
    az logout
    az login
  fi
else
  info "Azure にログインします..."
  az login
fi

# ----------------------------------------------------------------------------
# サブスクリプション選択
# ----------------------------------------------------------------------------
header "サブスクリプション選択"

echo "利用可能なサブスクリプション:"
az account list --query "[].{Name:name, ID:id, State:state}" -o table

echo ""
read -rp "使用するサブスクリプション ID を入力してください: " SUBSCRIPTION_ID

az account set --subscription "${SUBSCRIPTION_ID}"
SUBSCRIPTION_NAME=$(az account show --query name -o tsv)
success "サブスクリプション設定完了: ${SUBSCRIPTION_NAME} (${SUBSCRIPTION_ID})"

# ----------------------------------------------------------------------------
# リソースグループ作成
# ----------------------------------------------------------------------------
header "リソースグループ作成"

echo "推奨リージョン:"
echo "  japaneast  (東日本)"
echo "  japanwest  (西日本)"
echo "  eastus     (米国東部)"
echo ""
read -rp "リージョンを入力してください [デフォルト: japaneast]: " LOCATION
LOCATION="${LOCATION:-japaneast}"

DEV_RG="finflow-dev-rg"
PROD_RG="finflow-prod-rg"

for RG in "${DEV_RG}" "${PROD_RG}"; do
  if az group show --name "${RG}" &>/dev/null; then
    warn "リソースグループ '${RG}' は既に存在します (スキップ)"
  else
    az group create --name "${RG}" --location "${LOCATION}" --output none
    success "リソースグループ作成完了: ${RG}"
  fi
done

# ----------------------------------------------------------------------------
# サービスプリンシパル作成
# ----------------------------------------------------------------------------
header "GitHub Actions 用サービスプリンシパル作成"

SP_NAME="finflow-github-actions-sp"
DEV_RG_ID=$(az group show --name "${DEV_RG}" --query id -o tsv)
PROD_RG_ID=$(az group show --name "${PROD_RG}" --query id -o tsv)

info "サービスプリンシパル '${SP_NAME}' を作成中..."

# 既存の SP があれば削除して再作成
EXISTING_SP=$(az ad sp list --display-name "${SP_NAME}" --query "[0].id" -o tsv 2>/dev/null || true)
if [[ -n "${EXISTING_SP}" ]]; then
  warn "既存のサービスプリンシパルが見つかりました。削除して再作成します..."
  az ad sp delete --id "${EXISTING_SP}" --output none
fi

SP_OUTPUT=$(az ad sp create-for-rbac \
  --name "${SP_NAME}" \
  --role Contributor \
  --scopes "${DEV_RG_ID}" "${PROD_RG_ID}" \
  --output json)

AZURE_CLIENT_ID=$(echo "${SP_OUTPUT}" | jq -r '.appId')
AZURE_CLIENT_SECRET=$(echo "${SP_OUTPUT}" | jq -r '.password')
AZURE_TENANT_ID=$(echo "${SP_OUTPUT}" | jq -r '.tenant')

success "サービスプリンシパル作成完了"

# ----------------------------------------------------------------------------
# OIDC フェデレーション資格情報の設定
# ----------------------------------------------------------------------------
header "OIDC フェデレーション資格情報の設定"

read -rp "GitHub リポジトリ (owner/repo 形式): " GITHUB_REPO

APP_OBJECT_ID=$(az ad app show --id "${AZURE_CLIENT_ID}" --query id -o tsv)

upsert_federated_credential() {
  local cred_name="$1"
  local subject="$2"
  local params="{
    \"name\": \"${cred_name}\",
    \"issuer\": \"https://token.actions.githubusercontent.com\",
    \"subject\": \"${subject}\",
    \"audiences\": [\"api://AzureADTokenExchange\"]
  }"

  if az ad app federated-credential show --id "${APP_OBJECT_ID}" --federated-credential-id "${cred_name}" &>/dev/null; then
    info "既存の資格情報を更新中: ${cred_name}"
    az ad app federated-credential update \
      --id "${APP_OBJECT_ID}" \
      --federated-credential-id "${cred_name}" \
      --parameters "${params}" --output none
  else
    info "資格情報を新規作成中: ${cred_name}"
    az ad app federated-credential create \
      --id "${APP_OBJECT_ID}" \
      --parameters "${params}" --output none
  fi
  success "${cred_name} 完了 (subject: ${subject})"
}

upsert_federated_credential "finflow-main-branch"     "repo:${GITHUB_REPO}:ref:refs/heads/main"
upsert_federated_credential "finflow-develop-branch"  "repo:${GITHUB_REPO}:ref:refs/heads/develop"
upsert_federated_credential "finflow-prod-environment" "repo:${GITHUB_REPO}:environment:production"
upsert_federated_credential "finflow-dev-environment"  "repo:${GITHUB_REPO}:environment:development"

success "OIDC フェデレーション資格情報の設定完了"

# ----------------------------------------------------------------------------
# シークレット生成
# ----------------------------------------------------------------------------
header "シークレット生成"

info "JWT キーを生成中..."
DEV_JWT_KEY=$(openssl rand -base64 48)
PROD_JWT_KEY=$(openssl rand -base64 48)

info "SQL 管理者パスワードを生成中..."
DEV_SQL_PASSWORD=$(openssl rand -base64 24 | tr -d '/+=')Aa1!
PROD_SQL_PASSWORD=$(openssl rand -base64 24 | tr -d '/+=')Bb2@

# ----------------------------------------------------------------------------
# .env ファイルに保存 (gitignore対象)
# ----------------------------------------------------------------------------
ENV_FILE="$(dirname "${BASH_SOURCE[0]}")/.env.setup"

cat > "${ENV_FILE}" << EOF
# FinFlow Azure セットアップ値 - このファイルはコミットしないでください
# 生成日時: $(date -u +"%Y-%m-%dT%H:%M:%SZ")

SUBSCRIPTION_ID="${SUBSCRIPTION_ID}"
LOCATION="${LOCATION}"
DEV_RESOURCE_GROUP="${DEV_RG}"
PROD_RESOURCE_GROUP="${PROD_RG}"

AZURE_CLIENT_ID="${AZURE_CLIENT_ID}"
AZURE_TENANT_ID="${AZURE_TENANT_ID}"

DEV_JWT_KEY="${DEV_JWT_KEY}"
PROD_JWT_KEY="${PROD_JWT_KEY}"
DEV_SQL_ADMIN_PASSWORD="${DEV_SQL_PASSWORD}"
PROD_SQL_ADMIN_PASSWORD="${PROD_SQL_PASSWORD}"
EOF

chmod 600 "${ENV_FILE}"
success "シークレットを ${ENV_FILE} に保存しました"

# ----------------------------------------------------------------------------
# GitHub Secrets 設定ガイド出力
# ----------------------------------------------------------------------------
header "GitHub Secrets 設定手順"

echo -e "${BOLD}以下の Secrets を GitHub リポジトリに設定してください:${NC}"
echo -e "Settings > Secrets and variables > Actions > New repository secret\n"

echo -e "${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo ""

echo -e "${BOLD}1. AZURE_CLIENT_ID${NC}"
echo "値: ${AZURE_CLIENT_ID}"
echo ""

echo -e "${BOLD}2. AZURE_TENANT_ID${NC}"
echo "値: ${AZURE_TENANT_ID}"
echo ""

echo -e "${BOLD}3. AZURE_SUBSCRIPTION_ID${NC}"
echo "値: ${SUBSCRIPTION_ID}"
echo ""

echo -e "${BOLD}4. DEV_JWT_KEY${NC}"
echo "値: ${DEV_JWT_KEY}"
echo ""

echo -e "${BOLD}5. PROD_JWT_KEY${NC}"
echo "値: ${PROD_JWT_KEY}"
echo ""

echo -e "${BOLD}6. DEV_SQL_ADMIN_PASSWORD${NC}"
echo "値: ${DEV_SQL_PASSWORD}"
echo ""

echo -e "${BOLD}7. PROD_SQL_ADMIN_PASSWORD${NC}"
echo "値: ${PROD_SQL_PASSWORD}"
echo ""

echo -e "${BOLD}8. SMTP_HOST${NC} (任意 - メール通知を使う場合)"
echo "値: SMTPサーバーのホスト名 (例: smtp.sendgrid.net)"
echo ""

echo -e "${BOLD}9. SMTP_USERNAME${NC} (任意)"
echo "値: SMTPユーザー名"
echo ""

echo -e "${BOLD}10. SMTP_PASSWORD${NC} (任意)"
echo "値: SMTPパスワード"
echo ""

echo -e "${BOLD}11. SMTP_FROM_ADDRESS${NC} (任意)"
echo "値: 送信元メールアドレス (例: noreply@yourdomain.com)"
echo ""

echo -e "${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo ""

echo -e "${GREEN}${BOLD}セットアップ完了！${NC}"
echo ""
echo "次のステップ:"
echo "  1. 上記の Secrets を GitHub に設定する"
echo "  2. develop ブランチにプッシュすると dev 環境に自動デプロイされます"
echo "  3. main ブランチにプッシュすると prod 環境に自動デプロイされます"
echo "  4. 手動デプロイ: ./infra/scripts/deploy.sh dev  または  ./infra/scripts/deploy.sh prod"
echo ""
echo -e "${YELLOW}注意: ${ENV_FILE} にシークレットが保存されています。安全に管理してください。${NC}"
