# 磁盘文件管理器 - PowerShell 启动脚本
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  磁盘文件管理器" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "正在启动程序..." -ForegroundColor Yellow

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ExePath = Join-Path $ScriptDir "DiskManager.exe"

if (Test-Path $ExePath) {
    Start-Process -FilePath $ExePath
    Write-Host "✓ 程序已启动" -ForegroundColor Green
} else {
    Write-Host "错误: 找不到 DiskManager.exe" -ForegroundColor Red
    Read-Host "按回车键退出"
}
