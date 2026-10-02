using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
            var cache = Path.Combine(temporary, "cache");
            var tool = EmbeddedSevenZip.ExtractTo(cache)!;
            var toolHash = ArchiveService.HashFile(tool);
            var toolDirectory = Path.GetDirectoryName(tool)!;
            Check(Directory.GetFiles(toolDirectory).Length == 4, "首次启动应释放全部内置组件");
            var writtenAt = File.GetLastWriteTimeUtc(tool);
            EmbeddedSevenZip.ExtractTo(cache);
            Check(File.GetLastWriteTimeUtc(tool) == writtenAt, "完整缓存不应重复写入");
            File.WriteAllText(tool, "damaged-cache");
            File.Delete(Path.Combine(toolDirectory, "7z.dll"));
            EmbeddedSevenZip.ExtractTo(cache);
            Check(ArchiveService.HashFile(tool) == toolHash && File.Exists(Path.Combine(toolDirectory, "7z.dll")),
                "缓存损坏和组件缺失时应恢复内置版本");
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
            foreach (var key in new[] { "StickerPortrait", "CrayonPortrait", "PixelPortrait" })
            {
                var portrait = (BitmapSource)window.Resources[key];
                Check(portrait.PixelWidth == 204 && portrait.PixelHeight == 204 && portrait.IsFrozen,
                    $"{key}: 画像应按显示尺寸解码并冻结");
            }
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

            CheckToggleCommands(temporary, archives, window.Dispatcher);
            window.Close();
            Console.WriteLine("工具缓存释放、复用和修复通过；画像解码、四种布局、界面启停与冲突预览通过。");
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

    private static void CheckToggleCommands(string temporary, ArchiveService archives, Dispatcher dispatcher)
    {
        var game = Path.Combine(temporary, "game");
        Directory.CreateDirectory(game);
        File.WriteAllText(Path.Combine(game, Games.Exe(GameId.World)), "fixture");
        var zipPath = Path.Combine(temporary, "ui-toggle.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        using (var writer = new StreamWriter(zip.CreateEntry("nativePC/plugins/ui.txt").Open()))
            writer.Write("ui-command");
        var data = Path.Combine(temporary, "ui-data");
        var manager = new ModManager(data, archives);
        Task.Run(() => manager.ImportAsync(zipPath, GameId.World, game)).GetAwaiter().GetResult();
        var model = new MainViewModel(manager, archives);
        model.ToggleModCommand.Execute(model.Mods.Single());
        WaitForCommand(model, dispatcher);
        var target = Path.Combine(game, "nativePC", "plugins", "ui.txt");
        Check(model.Mods.Single().Enabled && File.ReadAllText(target) == "ui-command",
            "界面启用后应同步列表和部署内容");
        model.Analysis = new PackageAnalysis
        {
            ArchivePath = zipPath,
            Variants = [new PackageVariant { Files = [new PackageFile { Source = "UI.txt", Target = "nativePC/plugins/UI.txt" }] }]
        };
        model.SelectedVariantIndex = 0;
        Check(model.PreviewText.Contains("与 ui-toggle 冲突"), "冲突预览应忽略路径大小写");
        model.ToggleModCommand.Execute(model.Mods.Single());
        WaitForCommand(model, dispatcher);
        Check(!model.Mods.Single().Enabled && !File.Exists(target), "界面停用后应撤回文件并同步列表");
        Check(!new ModManager(data, archives).Mods.Single().Enabled, "界面停用结果应持久保存");
    }

    private static void WaitForCommand(MainViewModel model, Dispatcher dispatcher)
    {
        if (!model.IsBusy) return;
        var frame = new DispatcherFrame();
        var started = DateTime.UtcNow;
        var timedOut = false;
        var timer = new DispatcherTimer(TimeSpan.FromMilliseconds(20), DispatcherPriority.Background,
            (_, _) =>
            {
                timedOut = DateTime.UtcNow - started > TimeSpan.FromSeconds(20);
                if (!model.IsBusy || timedOut) frame.Continue = false;
            }, dispatcher);
        try { Dispatcher.PushFrame(frame); }
        finally { timer.Stop(); }
        Check(!timedOut, "界面命令执行超时");
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
