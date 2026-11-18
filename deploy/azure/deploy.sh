#!/usr/bin/env bash
set -euo pipefail

: "${NEON_CONNECTION_STRING:?Set NEON_CONNECTION_STRING (PostgreSQL connection string with pgvector)}"
: "${OPENAI_API_KEY:?Set OPENAI_API_KEY}"

RG="${AZURE_RESOURCE_GROUP:-rg-jobfit-copilot}"
LOC="${AZURE_LOCATION:-eastus}"
ENV_NAME="${AZURE_ACA_ENV:-cae-jobfit}"
GHCR_OWNER="${GHCR_OWNER:-hellseyfer}"
API_IMAGE="ghcr.io/${GHCR_OWNER}/job-fit-copilot-api:latest"
WEB_IMAGE="ghcr.io/${GHCR_OWNER}/job-fit-copilot-web:latest"
LLM_MODEL="${LLM_MODEL:-gpt-4o-mini}"
EMBEDDING_MODEL="${EMBEDDING_MODEL:-text-embedding-3-small}"

az extension add --name containerapp --upgrade --only-show-errors >/dev/null
az group create -n "$RG" -l "$LOC" --only-show-errors >/dev/null

if ! az containerapp env show -n "$ENV_NAME" -g "$RG" >/dev/null 2>&1; then
  az containerapp env create -n "$ENV_NAME" -g "$RG" -l "$LOC" --only-show-errors >/dev/null
fi

CHAT_CONN="Endpoint=https://api.openai.com/v1;Key=${OPENAI_API_KEY};Model=${LLM_MODEL}"
EMBED_CONN="Endpoint=https://api.openai.com/v1;Key=${OPENAI_API_KEY};Model=${EMBEDDING_MODEL}"

if az containerapp show -n jobfit-api -g "$RG" >/dev/null 2>&1; then
  az containerapp secret set -n jobfit-api -g "$RG" \
    --secrets "pg-conn=$NEON_CONNECTION_STRING" "chat-conn=$CHAT_CONN" "embed-conn=$EMBED_CONN" \
    --only-show-errors >/dev/null
  az containerapp update -n jobfit-api -g "$RG" \
    --image "$API_IMAGE" \
    --min-replicas 0 --max-replicas 1 \
    --only-show-errors >/dev/null
else
  az containerapp create -n jobfit-api -g "$RG" --environment "$ENV_NAME" \
    --image "$API_IMAGE" \
    --ingress internal --target-port 8080 \
    --min-replicas 0 --max-replicas 1 \
    --secrets "pg-conn=$NEON_CONNECTION_STRING" "chat-conn=$CHAT_CONN" "embed-conn=$EMBED_CONN" \
    --env-vars \
      ASPNETCORE_ENVIRONMENT=Production \
      ConnectionStrings__jobfit=secretref:pg-conn \
      Llm__Provider=openai \
      ConnectionStrings__chat=secretref:chat-conn \
      ConnectionStrings__embeddings=secretref:embed-conn \
    --only-show-errors >/dev/null
fi

API_FQDN=$(az containerapp show -n jobfit-api -g "$RG" --query properties.configuration.ingress.fqdn -o tsv)
API_ORIGIN="https://${API_FQDN}"

if az containerapp show -n jobfit-web -g "$RG" >/dev/null 2>&1; then
  az containerapp update -n jobfit-web -g "$RG" \
    --image "$WEB_IMAGE" \
    --set-env-vars "API_ORIGIN=$API_ORIGIN" \
    --min-replicas 0 --max-replicas 1 \
    --only-show-errors >/dev/null
else
  az containerapp create -n jobfit-web -g "$RG" --environment "$ENV_NAME" \
    --image "$WEB_IMAGE" \
    --ingress external --target-port 80 \
    --min-replicas 0 --max-replicas 1 \
    --env-vars "API_ORIGIN=$API_ORIGIN" \
    --only-show-errors >/dev/null
fi

WEB_FQDN=$(az containerapp show -n jobfit-web -g "$RG" --query properties.configuration.ingress.fqdn -o tsv)
echo "Deployed Job Fit Copilot:"
echo "  Web UI: https://${WEB_FQDN}"
echo "  API (internal): ${API_ORIGIN}"
