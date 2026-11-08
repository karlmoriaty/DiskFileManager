using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using WpfMessageBox = System.Windows.MessageBox;
using WpfMessageBoxButton = System.Windows.MessageBoxButton;
using WpfMessageBoxImage = System.Windows.MessageBoxImage;

namespace DiskManager;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private string? _diagnosticLogPath;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        SetupDiagnosticLogging();
        WriteDiagnosticLog("应用程序启动开始");

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        try
        {
            WriteDiagnosticLog("初始化主窗口");
            var mainWindow = new MainWindow();
            mainWindow.Show();
            WriteDiagnosticLog("主窗口显示成功");
        }
        catch (Exception ex)
        {
            WriteDiagnosticLog($"主窗口初始化失败: {ex}");
            ShowStartupError(ex);
        }
    }

    private void SetupDiagnosticLogging()
    {
        try
        {
            var tempPath = Path.GetTempPath();
            var appDataPath = Path.Combine(tempPath, "DiskManager_Diagnostics");
            
            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
            }

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            _diagnosticLogPath = Path.Combine(appDataPath, $"startup_{timestamp}.log");
            
            WriteDiagnosticLog($"诊断日志文件: {_diagnosticLogPath}");
            WriteDiagnosticLog($"操作系统: {Environment.OSVersion}");
            WriteDiagnosticLog($".NET版本: {Environment.Version}");
            WriteDiagnosticLog($"用户目录: {Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}");
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show($"无法设置诊断日志: {ex.Message}", "诊断警告", 
                WpfMessageBoxButton.OK, WpfMessageBoxImage.Warning);
        }
    }

    private void WriteDiagnosticLog(string message)
    {
        try
        {
            var logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
            
            if (!string.IsNullOrEmpty(_diagnosticLogPath))
            {
                File.AppendAllText(_diagnosticLogPath, logLine + Environment.NewLine);
            }
            
            System.Diagnostics.Debug.WriteLine(logLine);
        }
        catch
        {
        }
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            WriteDiagnosticLog($"未处理的异常: {ex}");
            ShowError("未处理的异常", ex);
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        WriteDiagnosticLog($"Dispatcher未处理的异常: {e.Exception}");
        ShowError("Dispatcher异常", e.Exception);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
        WriteDiagnosticLog($"未观察到的任务异常: {e.Exception}");
    }

    private void ShowStartupError(Exception ex)
    {
        var errorMessage = $"应用程序启动失败！\n\n" +
                          $"错误信息: {ex.Message}\n\n" +
                          $"堆栈跟踪:\n{ex.StackTrace}\n\n" +
                          $"诊断日志位置: {_diagnosticLogPath ?? "未创建"}";
        
        WpfMessageBox.Show(errorMessage, "启动失败", 
            WpfMessageBoxButton.OK, WpfMessageBoxImage.Error);
        
        Shutdown();
    }

    private void ShowError(string title, Exception ex)
    {
        var errorMessage = $"{title}\n\n" +
                          $"错误信息: {ex.Message}\n\n" +
                          $"诊断日志位置: {_diagnosticLogPath ?? "未创建"}";
        
        WpfMessageBox.Show(errorMessage, title, 
            WpfMessageBoxButton.OK, WpfMessageBoxImage.Error);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        WriteDiagnosticLog($"应用程序退出，退出代码: {e.ApplicationExitCode}");
        base.OnExit(e);
    }
}

