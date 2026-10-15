# 把某次构建的产物发布到 Bundles\CDN（服务器目录）
# 用法：
#   powershell -ExecutionPolicy Bypass -File Tools\publish.ps1            # 自动取最新版本目录
#   powershell -ExecutionPolicy Bypass -File Tools\publish.ps1 1.1        # 指定版本号
param([string]$Version)

$proj   = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$gameRes = Join-Path $proj 'Bundles\StandaloneWindows64\GameRes'
$cdn    = Join-Path $proj 'Bundles\CDN'

if ([string]::IsNullOrEmpty($Version)) {
    $ver = Get-ChildItem $gameRes -Directory |
        Where-Object { $_.Name -notin @('OutputCache', 'Simulate') } |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($ver -eq $null) { Write-Host "找不到任何版本目录：$gameRes" -ForegroundColor Red; exit 1 }
    $Version = $ver.Name
}

$src = Join-Path $gameRes $Version
if (!(Test-Path -LiteralPath $src)) { Write-Host "找不到版本目录：$src" -ForegroundColor Red; exit 1 }

New-Item -ItemType Directory -Force -Path $cdn | Out-Null
# 覆盖版本指针 GameRes.version 和清单；新增的 AB 包按内容哈希命名，不会和旧包冲突（旧包保留）
Copy-Item (Join-Path $src '*') $cdn -Recurse -Force

Write-Host "已发布版本 [$Version] 到 $cdn" -ForegroundColor Green
Write-Host "现在可以重启客户端测试热更了。" -ForegroundColor Green
