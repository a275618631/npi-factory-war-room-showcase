param(
    [string]$PostgresPort = $env:POSTGRES_PORT
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

# 1. Check Docker is running
try {
    docker info > $null 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Error "錯誤: Docker 未在運行，請先啟動 Docker 或 Docker Desktop。"
        exit 1
    }
} catch {
    Write-Error "錯誤: 無法執行 Docker 指令，請確認 Docker 已安裝並加入 PATH。"
    exit 1
}

if (-not $PostgresPort) {
    $portInUse = $false
    try {
        $tcpClient = New-Object System.Net.Sockets.TcpClient
        $tcpClient.Connect("127.0.0.1", 5432)
        $tcpClient.Close()
        $portInUse = $true
    } catch {
        $portInUse = $false
    }

    if ($portInUse) {
        $PostgresPort = "5433"
    } else {
        $PostgresPort = "5432"
    }
}

$env:POSTGRES_PORT = $PostgresPort
Write-Host "啟動 NPI 平台環境 (PostgreSQL 埠: $PostgresPort)..."
docker compose up -d --build

Write-Host "等待後端與前端健康檢查通過..."
$maxWait = 300
$waited = 0
$backendHealthy = $false
$frontendReady = $false

while ($waited -lt $maxWait) {
    if (-not $backendHealthy) {
        try {
            $res = Invoke-WebRequest -Uri "http://localhost:5000/health" -UseBasicParsing -TimeoutSec 2 -ErrorAction SilentlyContinue
            if ($res.StatusCode -eq 200) {
                $backendHealthy = $true
            }
        } catch { }
    }

    if (-not $frontendReady) {
        try {
            $res = Invoke-WebRequest -Uri "http://localhost:5173/" -UseBasicParsing -TimeoutSec 2 -ErrorAction SilentlyContinue
            if ($res.StatusCode -eq 200) {
                $frontendReady = $true
            }
        } catch { }
    }

    if ($backendHealthy -and $frontendReady) {
        break
    }

    Start-Sleep -Seconds 2
    $waited += 2
}

if (-not ($backendHealthy -and $frontendReady)) {
    Write-Warning "警告: 等待超時 (${maxWait}s)，請檢查 docker compose logs。"
    docker compose ps
    exit 1
}

Write-Host "=================================================="
Write-Host "  NPI 專案管理平台已成功啟動！"
Write-Host ""
Write-Host "  網址：        http://localhost:5173"
Write-Host "  後端 API：    http://localhost:5000"
Write-Host "  健康檢查：    http://localhost:5000/health"
Write-Host "  示範帳號：    demo"
Write-Host "  示範密碼：    使用你設定的 DEMO_USER_PASSWORD"
Write-Host "=================================================="


