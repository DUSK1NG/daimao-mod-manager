using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using HunterModManager.Core;
using HunterModManager.Wpf;
using HunterModManager.Wpf.ViewModels;
using HunterModManager.Wpf.Views;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var temporary = Path.Combine(Path.GetTempPath(), "HunterModManager.WpfLayoutTests", Guid.NewGuid().ToString("N"));
        try
        {
            var sevenZip = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "7-Zip", "7z.exe");
            var archives = new ArchiveService(sevenZip);
            var app = new App();
            app.InitializeComponent();
            var viewModel = new MainViewModel(new ModManager(temporary, archives), archives);
            var demo = args.Contains("--preview", StringComparer.OrdinalIgnoreCase);
            var window = new MainWindow
            {
                DataContext = viewModel,
                ShowInTaskbar = demo,
                Opacity = demo ? 1 : 0
            };
            if (demo)
            {
                viewModel.ArchivePath = @"C:\示例\hunter-pack.zip";
                viewModel.Analysis = new PackageAnalysis
                {
                    ArchivePath = viewModel.ArchivePath,
                    Variants = [new PackageVariant { Name = "默认" }]
                };
                viewModel.VariantNames.Add("默认");
                viewModel.SelectedVariantIndex = 0;
                viewModel.PreviewText = "包：hunter-pack.zip\nSHA-256：示例\n文件：2 个\n  nativePC/plugins/example.dll → 游戏目录/nativePC/plugins/example.dll [新文件]";
                app.Run(window);
                return 0;
            }
            window.Show();
            Check(Find<TextBlock>(window, x => x.Text == "尚无 Mod").IsVisible,
                "空列表提示不可见");
            var advanced = Find<Button>(window, x => Equals(x.Content, "高级选项：手动映射"));
            var command = advanced.Command ?? throw new InvalidOperationException("手动映射入口未绑定命令");
            command.Execute(null);
            Check(viewModel.ShowManualMapping, "手动映射入口没有展开选项");
            command.Execute(null);

            foreach (var (width, height, maxListWidth) in new[]
                     {
                         (1100d, 760d, 300d), (800d, 760d, 220d),
                         (700d, 720d, 220d), (640d, 540d, 220d)
                     })
            {
                window.Width = width;
                window.Height = height;
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                window.UpdateLayout();
                var list = Find<Border>(window, x => x.Name == "ModPane");
                Check(list.ActualWidth <= maxListWidth, $"{width}×{height}: Mod 列表过宽");
                var mascots = Find<Border>(window, x => x.Name == "MascotStrip");
                Check(mascots.IsVisible == (width >= 680 && height >= 650),
                    $"{width}×{height}: 呆猫画像栏显示状态错误");
                if (mascots.IsVisible)
                {
                    var bounds = mascots.TransformToAncestor(window).TransformBounds(new Rect(mascots.RenderSize));
                    Check(bounds.Right <= window.ActualWidth && bounds.Bottom <= window.ActualHeight,
                        $"{width}×{height}: 呆猫画像栏超出窗口");
                    Check(Find<TextBlock>(mascots, x => x.Text == "老大，包给我瞅瞅？").IsVisible,
                        $"{width}×{height}: 呆猫文案不可见");
                }
                CheckVisible(window, "更改目录");
                CheckVisible(window, "选择压缩包");
                CheckVisible(window, "导入所选版本  ·  默认停用");
            }

            window.Close();
            Console.WriteLine("WPF 布局测试通过：1100×760、800×760、700×720、640×540 的主要操作可见。");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
        finally
        {
            if (Directory.Exists(temporary)) Directory.Delete(temporary, true);
        }
    }

    private static void CheckVisible(Window window, string content)
    {
        var button = Find<Button>(window, x => Equals(x.Content, content));
        Check(button.IsVisible && button.ActualWidth > 0 && button.ActualHeight > 0,
            $"{window.Width}×{window.Height}: {content} 不可见");
        var bounds = button.TransformToAncestor(window).TransformBounds(new Rect(button.RenderSize));
        Check(bounds.Left >= 0 && bounds.Top >= 0 && bounds.Right <= window.ActualWidth &&
              bounds.Bottom <= window.ActualHeight,
            $"{window.Width}×{window.Height}: {content} 超出窗口");
    }

    private static T Find<T>(DependencyObject root, Func<T, bool> match) where T : DependencyObject
    {
        if (root is T candidate && match(candidate)) return candidate;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            try { return Find(VisualTreeHelper.GetChild(root, i), match); }
            catch (InvalidOperationException) { }
        }
        throw new InvalidOperationException($"未找到 {typeof(T).Name} 控件");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
