using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Windows;
using HunterModManager.Core;
using HunterModManager.Wpf.ViewModels;
using HunterModManager.Wpf.Views;

namespace HunterModManager.Wpf;

internal static class Program
{
    internal static readonly string DataRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HunterModManager");

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length != 0) return RunHelper(args);
            ModManager manager;
            try { manager = CreateManager(); }
            catch (UnauthorizedAccessException)
            {
                if (!RunElevated("--elevated-recover", DataRoot)) return 1;
                manager = CreateManager();
            }

            var app = new App();
            app.InitializeComponent();
            var window = new MainWindow
            {
                DataContext = new MainViewModel(manager, new ArchiveService(FindSevenZip()))
            };
            app.Run(window);
            return 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "呆猫mod manager", MessageBoxButton.OK, MessageBoxImage.Error);
            return 1;
        }
    }

    private static int RunHelper(string[] args)
    {
        if (!IsAdministrator()) return 2;
        if (args.Length == 2 && args[0] == "--elevated-recover" && TryValidateDataRoot(args[1], out var recoverRoot))
        {
            _ = CreateManager(recoverRoot);
            return 0;
        }
        if (args.Length != 5 || args[0] != "--elevated-toggle" ||
            !TryValidateDataRoot(args[1], out var toggleRoot) ||
            !Regex.IsMatch(args[2], "\\A[0-9a-fA-F]{32}\\z") ||
            args[3] is not ("enable" or "disable") || args[4] is not ("switch" or "normal")) return 2;
        var manager = CreateManager(toggleRoot);
        manager.SetEnabledAsync(args[2], args[3] == "enable", args[4] == "switch").GetAwaiter().GetResult();
        return 0;
    }

    private static bool TryValidateDataRoot(string value, out string root)
    {
        root = "";
        try
        {
            if (!Path.IsPathFullyQualified(value)) return false;
            var full = Path.GetFullPath(value).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!Path.GetFileName(full).Equals("HunterModManager", StringComparison.OrdinalIgnoreCase) || !Directory.Exists(full)) return false;
            root = full;
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
    }

    internal static ModManager CreateManager(string? dataRoot = null)
        => new(dataRoot ?? DataRoot, new ArchiveService(FindSevenZip()));

    internal static string FindSevenZip()
    {
        var embedded = EmbeddedSevenZip.ExtractTo(DataRoot);
        if (embedded is not null) return embedded;
        var bundled = Path.Combine(AppContext.BaseDirectory, "tools", "7z.exe");
        if (File.Exists(bundled)) return bundled;
        var local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "7-Zip", "7z.exe");
        if (File.Exists(local)) return local;
        var x86 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "7-Zip", "7z.exe");
        if (File.Exists(x86)) return x86;
        throw new FileNotFoundException("找不到 7-Zip。请将 7z.exe 放在程序目录的 tools 文件夹。", bundled);
    }

    internal static bool RunElevated(params string[] args)
    {
        try
        {
            var executable = Environment.ProcessPath ?? throw new InvalidOperationException("无法确定当前程序路径");
            var start = new ProcessStartInfo(executable) { Verb = "runas", UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden };
            foreach (var arg in args) start.ArgumentList.Add(arg);
            using var process = Process.Start(start) ?? throw new InvalidOperationException("无法启动管理员辅助进程");
            process.WaitForExit();
            if (process.ExitCode == 0) return true;
            MessageBox.Show($"管理员辅助进程未完成操作（退出码 {process.ExitCode}）。请检查游戏是否已关闭和目标文件状态。",
                "操作失败", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            MessageBox.Show("已取消管理员权限请求。", "操作取消", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }
    }

    private static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }
}
