using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Principal;
using System.Windows;
using System.Windows.Navigation;

namespace DiskManager;

public partial class AboutWindow : Window
{
    private readonly string? _logDirectory;

    public AboutWindow(string? logDirectory)
    {
        InitializeComponent();
        _logDirectory = logDirectory;
        VersionText.Text = "版本 " + (Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "2.4.4");
        RuntimeText.Text = $"运行时 .NET {Environment.Version} · {Environment.OSVersion}";
        PrivilegeText.Text = IsElevated()
            ? "权限：管理员"
            : "权限：标准用户（操作系统目录时可能需要以管理员身份运行）";
        LogPathText.Text = logDirectory ?? "未创建";
    }

    private static bool IsElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private void OpenLogFolder_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_logDirectory) || !Directory.Exists(_logDirectory))
        {
            MessageBox.Show(this, "日志目录尚未创建。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            Process.Start("explorer.exe", $"\"{_logDirectory}\"");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception)
        {
        }

        e.Handled = true;
    }
}