using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace DiskManager;

public partial class App : Application
{
    private Mutex? _instanceMutex;
    private string? _diagnosticLogPath;

    protected override void OnStartup(StartupEventArgs e)
    {
        _instanceMutex = new Mutex(true, @"Local\DiskManager_SingleInstance", out var isFirstInstance);
        if (!isFirstInstance)
        {
            MessageBox.Show("磁盘文件管理器已经在运行。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);

        SetupDiagnosticLogging();
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        try
        {
            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            WriteDiagnosticLog($"主窗口初始化失败: {ex}");
            MessageBox.Show($"应用程序启动失败！\n\n错误信息: {ex.Message}\n\n诊断日志位置: {_diagnosticLogPath ?? "未创建"}",
                "启动失败", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    private void SetupDiagnosticLogging()
    {
        try
        {
            var directory = Path.Combine(Path.GetTempPath(), "DiskManager_Diagnostics");
            Directory.CreateDirectory(directory);
            _diagnosticLogPath = Path.Combine(directory, "startup_latest.log");
            File.WriteAllText(_diagnosticLogPath, string.Empty);
            WriteDiagnosticLog($"应用程序启动；系统 {Environment.OSVersion}；运行时 {Environment.Version}");
        }
        catch (Exception)
        {
        }
    }

    private void WriteDiagnosticLog(string message)
    {
        try
        {
            if (!string.IsNullOrEmpty(_diagnosticLogPath))
            {
                File.AppendAllText(_diagnosticLogPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
            }
        }
        catch (Exception)
        {
        }
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            WriteDiagnosticLog($"未处理的异常: {ex}");
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        WriteDiagnosticLog($"Dispatcher 未处理的异常: {e.Exception}");
        MessageBox.Show($"发生未处理的错误。\n\n{e.Exception.Message}\n\n诊断日志: {_diagnosticLogPath}",
            "错误", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
        WriteDiagnosticLog($"未观察到的任务异常: {e.Exception}");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        WriteDiagnosticLog($"应用程序退出，代码 {e.ApplicationExitCode}");
        _instanceMutex?.ReleaseMutex();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
}