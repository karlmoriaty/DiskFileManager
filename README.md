# 磁盘文件管理器 - 使用说明

## 📦 产品信息

- **程序名称**: 磁盘文件管理器
- **版本**: 1.0
- **类型**: 便携版（无需安装 .NET 运行时）
- **支持系统**: Windows 10/11 x64

---

## 🚀 快速开始

### 方法一：使用发布脚本（开发者）
1. 打开 PowerShell
2. 进入项目目录：`cd H:\workpast\DiskFileManager\DiskManager`
3. 运行发布命令：
   ```powershell
   dotnet publish --configuration Release --runtime win-x64 --self-contained true --output ..\DiskManager_Portable /p:PublishSingleFile=true /p:PublishTrimmed=true /p:TrimMode=link /p:DebuggerSupport=false /p:PublishReadyToRun=true
   ```
4. 将 `启动程序.bat`、`启动程序.ps1`、`使用说明.md` 复制到 `DiskManager_Portable` 文件夹

### 方法二：双击启动（最终用户）
1. 将整个 `DiskManager_Portable` 文件夹复制到 U 盘
2. 在目标电脑上插入 U 盘
3. 双击 `启动程序.bat` 即可启动

### 方法三：直接运行 EXE
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
