param(
    [switch]$Reset
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

if ($Reset -or ($args -contains "--reset")) {
    Write-Host "正在停止服務並清除資料庫磁碟區 (--reset)..."
    docker compose down -v
    Write-Host "服務已停止，資料庫磁碟區已重置。"
} else {
    Write-Host "正在停止服務..."
    docker compose down
    Write-Host "服務已停止。"
}

