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
