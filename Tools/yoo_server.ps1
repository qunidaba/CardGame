$ErrorActionPreference = 'Stop'

# 服务目录：项目根\Bundles\CDN（发布脚本会把每次构建的产物拷到这里）
$root = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\Bundles\CDN')).Path
$prefix = 'http://127.0.0.1:8000/'

$listener = New-Object System.Net.HttpListener
$listener.Prefixes.Add($prefix)
$listener.Start()
Write-Host "资源服务器已启动：$prefix" -ForegroundColor Green
Write-Host "根目录：$root" -ForegroundColor Green
Write-Host "关掉本窗口或按 Ctrl+C 停止。" -ForegroundColor DarkGray

while ($listener.IsListening) {
    $ctx = $listener.GetContext()
    try {
        $rel = [System.Uri]::UnescapeDataString($ctx.Request.Url.LocalPath.TrimStart('/'))
        $path = Join-Path $root $rel
        if (Test-Path -LiteralPath $path -PathType Leaf) {
            $bytes = [System.IO.File]::ReadAllBytes($path)
            $ctx.Response.ContentLength64 = $bytes.Length
            $ctx.Response.OutputStream.Write($bytes, 0, $bytes.Length)
            Write-Host ("200 " + $rel)
        } else {
            $ctx.Response.StatusCode = 404
            Write-Host ("404 " + $rel) -ForegroundColor Yellow
        }
    } catch {
        Write-Host ("ERR " + $_.Exception.Message) -ForegroundColor Red
    } finally {
        $ctx.Response.OutputStream.Close()
    }
}
