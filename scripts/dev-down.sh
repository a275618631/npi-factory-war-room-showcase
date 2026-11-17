#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

if [ "${1:-}" = "--reset" ]; then
  echo "正在停止服務並清除資料庫磁碟區 (--reset)..."
  docker compose down -v
  echo "服務已停止，資料庫磁碟區已重置。"
else
  echo "正在停止服務..."
  docker compose down
  echo "服務已停止。"
fi

