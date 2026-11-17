#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

# 1. 檢查 Docker 是否正常運作
if ! docker info >/dev/null 2>&1; then
  echo "錯誤: Docker 未在運行，請先啟動 Docker 或 Docker Desktop。" >&2
  exit 1
fi

# 自動偵測本機 5432 埠是否被佔用，避免與本地已存在的 PostgreSQL 衝突
if [ -z "${POSTGRES_PORT:-}" ]; then
  if (nc -z 127.0.0.1 5432 || (echo > /dev/tcp/127.0.0.1/5432)) >/dev/null 2>&1; then
    export POSTGRES_PORT=5433
  else
    export POSTGRES_PORT=5432
  fi
fi

echo "啟動 NPI 平台環境 (PostgreSQL 埠: ${POSTGRES_PORT})..."
docker compose up -d --build

echo "等待後端與前端健康檢查通過..."
max_wait=300
waited=0
backend_healthy=0
frontend_ready=0

while [ $waited -lt $max_wait ]; do
  if [ $backend_healthy -eq 0 ]; then
    if curl -s -f http://localhost:5000/health >/dev/null 2>&1; then
      backend_healthy=1
    fi
  fi

  if [ $frontend_ready -eq 0 ]; then
    if curl -s -f http://localhost:5173/ >/dev/null 2>&1; then
      frontend_ready=1
    fi
  fi

  if [ $backend_healthy -eq 1 ] && [ $frontend_ready -eq 1 ]; then
    break
  fi

  sleep 2
  waited=$((waited + 2))
done

if [ $backend_healthy -ne 1 ] || [ $frontend_ready -ne 1 ]; then
  echo "警告: 等待超時 (${max_wait}s)，請檢查 docker compose logs。" >&2
  docker compose ps
  exit 1
fi

echo "=================================================="
echo "  NPI 專案管理平台已成功啟動！"
echo ""
echo "  網址：        http://localhost:5173"
echo "  後端 API：    http://localhost:5000"
echo "  健康檢查：    http://localhost:5000/health"
echo "  示範帳號：    demo"
echo "  示範密碼：    使用你設定的 DEMO_USER_PASSWORD"
echo "=================================================="


