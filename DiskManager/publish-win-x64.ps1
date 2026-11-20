# 磁盘文件管理器 - Windows x64 便携版发布脚本
# 作者: 自动生成
# 日期: 2026-03-27

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectFile = Join-Path $ScriptDir "DiskManager.csproj"
$OutputDir = Join-Path $ScriptDir "..\DiskManager_Portable"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  磁盘文件管理器 - 便携版发布工具" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "[1/5] 检查项目文件..." -ForegroundColor Yellow
if (-not (Test-Path $ProjectFile)) {
    Write-Host "错误: 找不到项目文件 $ProjectFile" -ForegroundColor Red
    exit 1
}
Write-Host "✓ 项目文件存在" -ForegroundColor Green

Write-Host ""
Write-Host "[2/5] 清理旧的发布文件..." -ForegroundColor Yellow
if (Test-Path $OutputDir) {
    Remove-Item -Path $OutputDir -Recurse -Force
    Write-Host "✓ 已清理旧的发布目录" -ForegroundColor Green
}
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

Write-Host ""
Write-Host "[3/5] 执行 dotnet publish..." -ForegroundColor Yellow
Write-Host "  配置: Self-contained, Single-file, ReadyToRun" -ForegroundColor Gray
Write-Host "  平台: win-x64" -ForegroundColor Gray

dotnet publish $ProjectFile `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $OutputDir `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    /p:DebuggerSupport=false `
    /p:PublishReadyToRun=true

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "错误: dotnet publish 失败" -ForegroundColor Red
    exit 1
}
Write-Host "✓ 发布成功" -ForegroundColor Green

Write-Host ""
Write-Host "[4/5] 创建启动脚本和文档..." -ForegroundColor Yellow

$BatContent = @"
@echo off
chcp 65001 >nul
title 磁盘文件管理器
echo.
echo ========================================
echo   磁盘文件管理器
echo ========================================
echo.
echo 正在启动程序...
echo.

cd /d "%~dp0"

if exist "DiskManager.exe" (
    start "" "DiskManager.exe"
) else (
    echo 错误: 找不到 DiskManager.exe
    echo.
    pause
)
"@

$BatPath = Join-Path $OutputDir "启动程序.bat"
[System.IO.File]::WriteAllLines($BatPath, $BatContent, [System.Text.Encoding]::GetEncoding("GBK"))
Write-Host "✓ 已创建: 启动程序.bat" -ForegroundColor Green

$Ps1Content = @"
# 磁盘文件管理器 - PowerShell 启动脚本
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  磁盘文件管理器" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "正在启动程序..." -ForegroundColor Yellow

`$ScriptDir = Split-Path -Parent `$MyInvocation.MyCommand.Path
`$ExePath = Join-Path `$ScriptDir "DiskManager.exe"

if (Test-Path `$ExePath) {
    Start-Process -FilePath `$ExePath
    Write-Host "✓ 程序已启动" -ForegroundColor Green
} else {
    Write-Host "错误: 找不到 DiskManager.exe" -ForegroundColor Red
    Read-Host "按回车键退出"
}
"@

$Ps1Path = Join-Path $OutputDir "启动程序.ps1"
[System.IO.File]::WriteAllLines($Ps1Path, $Ps1Content, [System.Text.Encoding]::UTF8)
Write-Host "✓ 已创建: 启动程序.ps1" -ForegroundColor Green

$ReadmeContent = @"
# 磁盘文件管理器 - 使用说明

## 📦 产品信息

- **程序名称**: 磁盘文件管理器
- **版本**: 2.0
- **类型**: 便携版（无需安装 .NET 运行时）
- **支持系统**: Windows 10/11 x64

---

## 🚀 快速开始

### 方法一：双击启动（推荐）
1. 将整个 `DiskManager_Portable` 文件夹复制到 U 盘
2. 在目标电脑上插入 U 盘
3. 双击 `启动程序.bat` 即可启动

### 方法二：直接运行 EXE
1. 双击 `DiskManager.exe` 直接运行

---

## 📋 系统要求

- **操作系统**: Windows 10 版本 1607 或更高版本 / Windows 11
- **架构**: 64 位 (x64)
- **磁盘空间**: 约 100-150 MB
- **无需安装**: .NET 运行时已打包在内

---

## 💾 复制到 U 盘

1. 确保 U 盘有足够的空间（至少 200 MB）
2. 将整个 `DiskManager_Portable` 文件夹复制到 U 盘根目录或任意位置
3. 安全弹出 U 盘

---

## 🔧 功能说明

### 主要功能
- **磁盘概览**: 查看所有磁盘的空间使用情况
- **大文件扫描**: 快速识别占用空间大的文件
- **重复文件检测**: 查找并清理重复文件
- **清理建议**: 智能识别可安全删除的临时文件和缓存
- **文件操作**: 批量复制、移动、删除文件

---

## ⚠️ 注意事项

1. **首次运行**: 可能需要几秒钟启动时间（解压单文件）
2. **杀毒软件**: 部分杀毒软件可能误报，请添加信任
3. **数据安全**: 删除文件前请仔细确认，避免误删重要文件
4. **系统文件**: 程序会自动保护系统文件，请勿尝试绕过

---

## ❓ 常见问题

### Q: 程序无法启动怎么办？
A: 请确保您的 Windows 是 64 位系统，并且版本满足要求。

### Q: 可以在 macOS 或 Linux 上运行吗？
A: 不可以，当前版本使用 WPF 框架，仅支持 Windows。

### Q: 程序会写入数据到电脑吗？
A: 程序会在临时目录写入日志文件，不会修改系统设置。

### Q: 如何更新程序？
A: 下载新版本的便携包，替换整个文件夹即可。

---

## 📞 技术支持

如遇到问题，请检查：
1. Windows 版本是否符合要求
2. 是否有足够的磁盘空间
3. 杀毒软件是否阻止了程序运行

---

**祝您使用愉快！** 🎉
"@

$ReadmePath = Join-Path $OutputDir "使用说明.md"
[System.IO.File]::WriteAllLines($ReadmePath, $ReadmeContent, [System.Text.Encoding]::UTF8)
Write-Host "✓ 已创建: 使用说明.md" -ForegroundColor Green

Write-Host ""
Write-Host "[5/5] 验证发布结果..." -ForegroundColor Yellow
$ExeFile = Join-Path $OutputDir "DiskManager.exe"
if (Test-Path $ExeFile) {
    $fileSize = (Get-Item $ExeFile).Length / 1MB
    Write-Host "✓ 主程序文件存在，大小: $([math]::Round($fileSize, 2)) MB" -ForegroundColor Green
} else {
    Write-Host "警告: 未找到 DiskManager.exe" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  ✓ 发布完成！" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "输出目录: $OutputDir" -ForegroundColor White
Write-Host ""
Write-Host "下一步操作:" -ForegroundColor Yellow
Write-Host "  1. 打开输出目录验证文件" -ForegroundColor Gray
Write-Host "  2. 测试运行 DiskManager.exe" -ForegroundColor Gray
Write-Host "  3. 复制整个文件夹到 U 盘" -ForegroundColor Gray
Write-Host ""
