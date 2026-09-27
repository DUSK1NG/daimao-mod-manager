using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using HunterModManager.Core;
using HunterModManager.Wpf.Helpers;

namespace HunterModManager.Wpf.ViewModels;

/// <summary>
/// Root ViewModel for MainWindow. Coordinates game selection, mod list, import, and prerequisites.
/// </summary>
public sealed class MainViewModel : ViewModelBase
{
    /// <summary>Static GameId values for XAML x:Static binding.</summary>
    public static class GameIdValues
    {
        public static GameId World => GameId.World;
        public static GameId Rise => GameId.Rise;
        public static GameId Wilds => GameId.Wilds;
    }

    private readonly ModManager manager;
    private readonly ArchiveService archives;

    private GameId selectedGame;
    private string gameRoot = "";
    private string statusText = "就绪";
    private bool isBusy;
    private string archivePath = "";
    private string manualMapping = "";
    private bool showManualMapping;
    private PackageAnalysis? analysis;
    private int selectedVariantIndex = -1;
    private string previewText = "";
    private string logText = "";
    private ModItemViewModel? selectedMod;
    private int selectedTabIndex;

    public MainViewModel(ModManager manager, ArchiveService archives)
    {
        this.manager = manager;
        this.archives = archives;

        SelectGameCommand = new RelayCommand(o => { if (o is GameId g) SelectGame(g); });
        BrowseGameRootCommand = new RelayCommand(BrowseGameRoot);
        BrowseArchiveCommand = new AsyncRelayCommand(BrowseArchiveAsync, () => !IsBusy);
        ToggleManualMappingCommand = new RelayCommand(() => ShowManualMapping = !ShowManualMapping);
        AnalyzeCommand = new AsyncRelayCommand(AnalyzeAsync, () => !IsBusy && ArchivePath.Length > 0);
        ImportCommand = new AsyncRelayCommand(ImportAsync, () => !IsBusy && Analysis is not null && SelectedVariantIndex >= 0);
        RemoveSelectedCommand = new RelayCommand(RemoveSelected, () => SelectedMod is not null && !IsBusy);
        ToggleModCommand = new AsyncRelayCommand(ToggleModAsync, _ => !IsBusy);
        ConfigurePrerequisiteCommand = new AsyncRelayCommand(ConfigurePrerequisiteAsync, _ => !IsBusy);
        DropArchiveCommand = new AsyncRelayCommand(DropArchiveAsync, _ => !IsBusy);

        SelectGame(GameId.World);
    }

    // ═══════════ Properties ═══════════

    public GameId SelectedGame
    {
        get => selectedGame;
        set
        {
            if (!SetField(ref selectedGame, value)) return;
            OnPropertyChanged(nameof(IsWorldSelected));
            OnPropertyChanged(nameof(IsRiseSelected));
            OnPropertyChanged(nameof(IsWildsSelected));
        }
    }

    public bool IsWorldSelected => SelectedGame == GameId.World;
    public bool IsRiseSelected => SelectedGame == GameId.Rise;
    public bool IsWildsSelected => SelectedGame == GameId.Wilds;

    public string GameRoot
    {
        get => gameRoot;
        set => SetField(ref gameRoot, value);
    }

    public string StatusText
    {
        get => statusText;
        set => SetField(ref statusText, value);
    }

    public bool IsBusy
    {
        get => isBusy;
        set => SetField(ref isBusy, value);
    }

    public string ArchivePath
    {
        get => archivePath;
        set => SetField(ref archivePath, value);
    }

    public string ManualMapping
    {
        get => manualMapping;
        set => SetField(ref manualMapping, value);
    }

    public bool ShowManualMapping
    {
        get => showManualMapping;
        set => SetField(ref showManualMapping, value);
    }

    public PackageAnalysis? Analysis
    {
        get => analysis;
        set => SetField(ref analysis, value);
    }

    public int SelectedVariantIndex
    {
        get => selectedVariantIndex;
        set
        {
            if (SetField(ref selectedVariantIndex, value))
                UpdatePreview();
        }
    }

    public string PreviewText
    {
        get => previewText;
        set => SetField(ref previewText, value);
    }

    public string LogText
    {
        get => logText;
        set => SetField(ref logText, value);
    }

    public ModItemViewModel? SelectedMod
    {
        get => selectedMod;
        set
        {
            if (SetField(ref selectedMod, value))
            {
                if (value is not null) PreviewText = value.DetailLines;
            }
        }
    }

    public int SelectedTabIndex
    {
        get => selectedTabIndex;
        set => SetField(ref selectedTabIndex, value);
    }

    public ObservableCollection<ModItemViewModel> Mods { get; } = [];
    public ObservableCollection<string> VariantNames { get; } = [];
    public ObservableCollection<PrerequisiteItemViewModel> Prerequisites_ { get; } = [];

    // ═══════════ Commands ═══════════

    public ICommand SelectGameCommand { get; }
    public ICommand BrowseGameRootCommand { get; }
    public ICommand BrowseArchiveCommand { get; }
    public ICommand ToggleManualMappingCommand { get; }
    public ICommand AnalyzeCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand RemoveSelectedCommand { get; }
    public ICommand ToggleModCommand { get; }
    public ICommand ConfigurePrerequisiteCommand { get; }
    public ICommand DropArchiveCommand { get; }

    // ═══════════ Game Selection ═══════════

    private void SelectGame(GameId game)
    {
        SelectedGame = game;
        var known = manager.Mods.FirstOrDefault(x => x.Game == game);
        GameRoot = known is not null && Games.ValidateRoot(game, known.GameRoot) is null
            ? known.GameRoot : SteamLocator.Find(game) ?? "";
        ClearAnalysis();
        RefreshMods();
        RefreshPrerequisites();
        if (GameRoot.Length == 0)
            SetStatus("请选择游戏目录，目录中需存在 " + Games.Exe(game));
    }

    private void BrowseGameRoot()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "选择包含 " + Games.Exe(SelectedGame) + " 的游戏目录"
        };
        if (Directory.Exists(GameRoot)) dialog.InitialDirectory = GameRoot;
        if (dialog.ShowDialog() != true) return;
        if (Games.ValidateRoot(SelectedGame, dialog.FolderName) is { } error)
        {
            ShowError(error);
            return;
        }
        GameRoot = Path.GetFullPath(dialog.FolderName);
        RefreshMods();
        RefreshPrerequisites();
        UpdatePreview();
        SetStatus("已选择游戏目录");
    }

    // ═══════════ Archive / Import ═══════════

    private async Task BrowseArchiveAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "压缩包 (*.zip;*.rar;*.7z)|*.zip;*.rar;*.7z",
            Title = "选择 Mod 压缩包"
        };
        if (dialog.ShowDialog() == true)
            await OpenArchiveAsync(dialog.FileName);
    }

    private async Task DropArchiveAsync(object? parameter)
    {
        if (parameter is string path)
            await OpenArchiveAsync(path);
    }

    private async Task OpenArchiveAsync(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext is not ".zip" and not ".rar" and not ".7z")
        {
            ShowError("只支持 ZIP、RAR、7Z 压缩包。");
            return;
        }
        ArchivePath = path;
        await AnalyzeAsync();
    }

    private async Task AnalyzeAsync()
    {
        if (ArchivePath.Length == 0) { ShowError("请先选择压缩包。"); return; }
        if (Games.ValidateRoot(SelectedGame, GameRoot) is { } error) { ShowError(error); return; }
        await RunBusyAsync(async () =>
        {
            var manual = string.IsNullOrWhiteSpace(ManualMapping) ? null : ManualMapping.Trim();
            Analysis = await archives.AnalyzeAsync(ArchivePath, SelectedGame, manual);
            VariantNames.Clear();
            foreach (var v in Analysis.Variants) VariantNames.Add(v.Name);
            SelectedVariantIndex = VariantNames.Count == 1 ? 0 : -1;
            UpdatePreview();
            SetStatus($"分析完成：{Analysis.Variants.Count} 个安装版本");
        });
    }

    private async Task ImportAsync()
    {
        if (Analysis is null || ArchivePath.Length == 0 || SelectedVariantIndex < 0)
        {
            ShowError("请先分析压缩包并选择一个安装版本。");
            return;
        }
        var variant = Analysis.Variants[SelectedVariantIndex];
        if (variant.BlockedReason is not null) { ShowError(variant.BlockedReason); return; }
        await RunBusyAsync(async () =>
        {
            var manual = string.IsNullOrWhiteSpace(ManualMapping) ? null : ManualMapping.Trim();
            var record = await manager.ImportAsync(ArchivePath, SelectedGame, GameRoot, variant.Name, manual);
            RefreshMods();
            SetStatus($"已导入 {record.Name}；勾选后启用。");
        });
    }

    // ═══════════ Mod Toggle / Remove ═══════════

    private async Task ToggleModAsync(object? parameter)
    {
        if (parameter is not ModItemViewModel item) return;
        var wantEnabled = !item.Enabled;
        await RunBusyAsync(async () =>
        {
            try
            {
                await manager.SetEnabledAsync(item.Id, wantEnabled);
            }
            catch (InvalidOperationException ex) when (wantEnabled && ex.Message.StartsWith("目标文件与已启用 Mod 冲突：", StringComparison.Ordinal))
            {
                var result = MessageBox.Show(
                    ex.Message + "\n\n停用冲突 Mod 并启用当前 Mod？",
                    "文件冲突",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes) return;
                await ToggleCoreAsync(item.Id, wantEnabled, true);
            }
            catch (UnauthorizedAccessException)
            {
                await ToggleElevatedAsync(item.Id, wantEnabled, false);
            }
            manager.Reload();
            SetStatus((wantEnabled ? "已启用：" : "已停用：") + item.Name);
        });
        RefreshMods();
        RefreshPrerequisites();
        UpdatePreview();
    }

    private async Task ToggleCoreAsync(string id, bool enabled, bool switchConflicts)
    {
        try { await manager.SetEnabledAsync(id, enabled, switchConflicts); }
        catch (UnauthorizedAccessException) { await ToggleElevatedAsync(id, enabled, switchConflicts); }
    }

    private Task ToggleElevatedAsync(string id, bool enabled, bool switchConflicts)
    {
        if (!Program.RunElevated("--elevated-toggle", Program.DataRoot, id,
            enabled ? "enable" : "disable", switchConflicts ? "switch" : "normal"))
            throw new InvalidOperationException("管理员辅助进程未完成操作。");
        return Task.CompletedTask;
    }

    private void RemoveSelected()
    {
        if (SelectedMod is null) return;
        try
        {
            manager.Remove(SelectedMod.Id);
            RefreshMods();
            SetStatus("已移除导入记录。");
        }
        catch (Exception ex) { ShowError(ex.Message); }
    }

    // ═══════════ Prerequisites ═══════════

    private async Task ConfigurePrerequisiteAsync(object? parameter)
    {
        if (parameter is not PrerequisiteItemViewModel item) return;
        var kind = item.Kind;
        string? path = null;
        var temporary = false;

        if (kind != PrerequisiteKind.REFramework)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "压缩包 (*.zip;*.rar;*.7z)|*.zip;*.rar;*.7z",
                Title = "选择 " + Core.Prerequisites.Display(kind) + " 安装包"
            };
            if (dialog.ShowDialog() != true) return;
            path = dialog.FileName;
        }
        else
        {
            path = Path.Combine(Path.GetTempPath(), "hmm-reframework-" + Guid.NewGuid().ToString("N") + ".zip");
            temporary = true;
        }

        var selectedPath = path;
        await RunBusyAsync(async () =>
        {
            try
            {
                if (kind == PrerequisiteKind.REFramework)
                {
                    SetStatus("正在从官方 GitHub 下载 REFramework…");
                    await Core.Prerequisites.DownloadOfficialREFrameworkAsync(selectedPath);
                }
                var files = Core.Prerequisites.SelectFiles(kind, await archives.ListFilesAsync(selectedPath));
                var preview = files.Select(file =>
                {
                    var target = Path.Combine(GameRoot, file.Target.Replace('/', Path.DirectorySeparatorChar));
                    return target + (File.Exists(target) ? " [将备份原文件]" : " [新文件]");
                });
                var message = "仅部署以下文件：\n" + string.Join("\n", preview) + "\n\n继续导入并启用？";
                if (MessageBox.Show(message, Core.Prerequisites.Display(kind),
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
                var record = await manager.ImportPrerequisiteAsync(selectedPath, kind, SelectedGame, GameRoot);
                await ToggleCoreAsync(record.Id, true, false);
                manager.Reload();
                RefreshMods();
                RefreshPrerequisites();
                SetStatus("已部署前置组件；请启动游戏确认实际生效。");
            }
            finally
            {
                if (temporary && File.Exists(selectedPath)) File.Delete(selectedPath);
            }
        });
    }

    // ═══════════ Refresh Helpers ═══════════

    public void RefreshMods()
    {
        Mods.Clear();
        foreach (var mod in manager.Mods.Where(x => x.Game == SelectedGame &&
                     x.GameRoot.Equals(GameRoot, StringComparison.OrdinalIgnoreCase)))
        {
            Mods.Add(new ModItemViewModel(mod));
        }
    }

    private void RefreshPrerequisites()
    {
        Prerequisites_.Clear();
        if (Games.ValidateRoot(SelectedGame, GameRoot) is not null) return;
        foreach (var kind in Core.Prerequisites.ForGame(SelectedGame))
        {
            var vm = new PrerequisiteItemViewModel { Kind = kind };
            vm.Refresh(GameRoot);
            Prerequisites_.Add(vm);
        }
    }

    private void ClearAnalysis()
    {
        Analysis = null;
        ArchivePath = "";
        ShowManualMapping = false;
        VariantNames.Clear();
        SelectedVariantIndex = -1;
        PreviewText = "";
    }

    private void UpdatePreview()
    {
        if (Analysis is null) return;
        var lines = new List<string>
        {
            "包：" + Path.GetFileName(Analysis.ArchivePath),
            "SHA-256：" + Analysis.Sha256
        };
        lines.AddRange(Analysis.Notes.Select(x => "提示：" + x));

        if (SelectedVariantIndex < 0)
        {
            lines.Add(Analysis.Variants.Count > 1 ? "请选择一个安装版本。" : "没有可导入的文件。");
            PreviewText = string.Join(Environment.NewLine, lines);
            return;
        }
        var variant = Analysis.Variants[SelectedVariantIndex];
        if (variant.BlockedReason is not null) lines.Add("⛔ 阻止安装：" + variant.BlockedReason);
        if (variant.Requirements.Count > 0) lines.Add("🔧 前置：" + string.Join("；", variant.Requirements));
        lines.Add($"📦 文件：{variant.Files.Count} 个");
        foreach (var file in variant.Files)
        {
            var target = Path.Combine(GameRoot, file.Target.Replace('/', Path.DirectorySeparatorChar));
            var owner = manager.Mods.FirstOrDefault(m => m.Enabled && m.Game == SelectedGame &&
                m.GameRoot.Equals(GameRoot, StringComparison.OrdinalIgnoreCase) &&
                m.Files.Any(f => f.Target.Equals(file.Target, StringComparison.OrdinalIgnoreCase)));
            var state = owner is not null ? " 🔴 [与 " + owner.Name + " 冲突]"
                : File.Exists(target) ? " 🟡 [将备份原文件]" : " 🟢 [新文件]";
            lines.Add("  " + file.Source + "  →  " + target + state);
        }
        PreviewText = string.Join(Environment.NewLine, lines);
    }

    // ═══════════ Busy / Status / Error ═══════════

    private async Task RunBusyAsync(Func<Task> operation)
    {
        if (IsBusy) return;
        IsBusy = true;
        try { await operation(); }
        catch (Exception ex) { ShowError(ex.Message); }
        finally { IsBusy = false; }
    }

    private void SetStatus(string message)
    {
        StatusText = message;
        LogText += $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";
    }

    private void ShowError(string message)
    {
        SetStatus("错误：" + message);
        MessageBox.Show(message, "Hunter Mod Manager", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
