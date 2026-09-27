using System.Text;
using System.Text.RegularExpressions;
using HunterModManager.Core;
using Microsoft.Win32;

namespace HunterModManager.App;

public sealed class MainForm : Form
{
    private readonly ModManager manager;
    private readonly ArchiveService archives;
    private readonly ComboBox gameBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 170 };
    private readonly TextBox rootBox = new() { ReadOnly = true, Dock = DockStyle.Fill };
    private readonly ListView mods = new() { Dock = DockStyle.Fill, View = View.Details, CheckBoxes = true, FullRowSelect = true, HideSelection = false };
    private readonly TextBox archiveBox = new() { ReadOnly = true, Dock = DockStyle.Fill };
    private readonly TextBox mappingBox = new() { Dock = DockStyle.Fill, PlaceholderText = "可选：nativePC、natives、reframework/autorun…" };
    private readonly ComboBox variantBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly TextBox previewBox = new() { ReadOnly = true, Multiline = true, ScrollBars = ScrollBars.Both, WordWrap = false, Dock = DockStyle.Fill, Font = new Font("Consolas", 9) };
    private readonly FlowLayoutPanel prerequisitePanel = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
    private readonly TextBox logBox = new() { ReadOnly = true, Multiline = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
    private readonly ToolStripStatusLabel status = new() { Text = "就绪" };
    private PackageAnalysis? analysis;
    private string? archivePath;
    private bool syncing;
    private bool busy;

    private GameId Game => (GameId)Math.Max(0, gameBox.SelectedIndex);
    private string Root => rootBox.Text;

    public MainForm(ModManager manager, ArchiveService archives)
    {
        this.manager = manager;
        this.archives = archives;
        Text = "Hunter Mod Manager";
        MinimumSize = new Size(900, 650);
        Size = new Size(1100, 760);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Microsoft YaHei UI", 9);
        BuildLayout();
        foreach (var game in Enum.GetValues<GameId>()) gameBox.Items.Add(Games.Display(game));
        gameBox.SelectedIndexChanged += (_, _) => SelectGame();
        gameBox.SelectedIndex = 0;
        InstallDropTarget(this);
    }

    private void BuildLayout()
    {
        var page = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(10) };
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(page);
        var chooser = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 2 };
        chooser.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));
        chooser.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 185));
        chooser.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        chooser.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        chooser.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        chooser.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        chooser.Controls.Add(new Label { Text = "游戏", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        chooser.Controls.Add(gameBox, 1, 0);
        chooser.Controls.Add(new Label { Text = "游戏目录", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        chooser.Controls.Add(rootBox, 1, 1);
        chooser.SetColumnSpan(rootBox, 2);
        var browseRoot = new Button { Text = "选择目录…", Dock = DockStyle.Fill };
        browseRoot.Click += (_, _) => BrowseGameRoot();
        chooser.Controls.Add(browseRoot, 3, 1);
        page.Controls.Add(chooser, 0, 0);

        var split = new SplitContainer { Width = 1060, Panel1MinSize = 270, Panel2MinSize = 460, SplitterDistance = 360, Dock = DockStyle.Fill };
        page.Controls.Add(split, 0, 1);
        var left = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        left.Controls.Add(new Label { Text = "已导入 Mod（勾选启用）", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        mods.Columns.Add("Mod", 220);
        mods.Columns.Add("状态", 110);
        mods.ItemChecked += async (_, e) => await ToggleFromListAsync(e.Item);
        mods.SelectedIndexChanged += (_, _) => ShowSelectedMod();
        left.Controls.Add(mods, 0, 1);
        var removeButton = new Button { Text = "移除选中记录", Dock = DockStyle.Fill };
        removeButton.Click += (_, _) => RemoveSelected();
        left.Controls.Add(removeButton, 0, 2);
        split.Panel1.Controls.Add(left);
        InstallDropTarget(mods);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        var importTab = new TabPage("导入与预览");
        var prereqTab = new TabPage("前置环境");
        var logTab = new TabPage("操作记录");
        tabs.TabPages.AddRange([importTab, prereqTab, logTab]);
        split.Panel2.Controls.Add(tabs);
        var import = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 5, Padding = new Padding(8) };
        import.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76));
        import.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        import.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        import.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        import.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        import.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        import.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        import.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        import.Controls.Add(new Label { Text = "压缩包", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 0);
        import.Controls.Add(archiveBox, 1, 0);
        var browseArchive = new Button { Text = "选择…", Dock = DockStyle.Fill };
        browseArchive.Click += async (_, _) => await BrowseArchiveAsync();
        import.Controls.Add(browseArchive, 2, 0);
        import.Controls.Add(new Label { Text = "手动映射", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 1);
        import.Controls.Add(mappingBox, 1, 1);
        var analyzeButton = new Button { Text = "重新分析", Dock = DockStyle.Fill };
        analyzeButton.Click += async (_, _) => await AnalyzeAsync();
        import.Controls.Add(analyzeButton, 2, 1);
        import.Controls.Add(new Label { Text = "安装版本", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 2);
        variantBox.SelectedIndexChanged += (_, _) => ShowPreview();
        import.Controls.Add(variantBox, 1, 2);
        import.SetColumnSpan(variantBox, 2);
        import.Controls.Add(previewBox, 0, 3);
        import.SetColumnSpan(previewBox, 3);
        var importButton = new Button { Text = "导入所选版本（默认停用）", Dock = DockStyle.Fill };
        importButton.Click += async (_, _) => await ImportAsync();
        import.Controls.Add(importButton, 0, 4);
        import.SetColumnSpan(importButton, 3);
        importTab.Controls.Add(import);
        InstallDropTarget(importTab);
        InstallDropTarget(previewBox);

        prereqTab.Controls.Add(prerequisitePanel);
        logTab.Controls.Add(logBox);
        var strip = new StatusStrip();
        strip.Items.Add(status);
        Controls.Add(strip);
    }

    private void InstallDropTarget(Control control)
    {
        control.AllowDrop = true;
        control.DragEnter += (_, e) => e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true ? DragDropEffects.Copy : DragDropEffects.None;
        control.DragDrop += async (_, e) =>
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length == 1)
                await OpenArchiveAsync(files[0]);
            else ShowError("一次请拖入一个 ZIP、RAR 或 7Z 文件。");
        };
    }

    private void SelectGame()
    {
        var known = manager.Mods.FirstOrDefault(x => x.Game == Game);
        rootBox.Text = known is not null && Games.ValidateRoot(Game, known.GameRoot) is null
            ? known.GameRoot : SteamLocator.Find(Game) ?? "";
        analysis = null;
        archivePath = null;
        archiveBox.Clear();
        variantBox.Items.Clear();
        previewBox.Clear();
        RefreshMods();
        RefreshPrerequisites();
        if (Root.Length == 0) SetStatus("请选择游戏目录，目录中需存在 " + Games.Exe(Game));
    }

    private void BrowseGameRoot()
    {
        using var dialog = new FolderBrowserDialog { Description = "选择包含 " + Games.Exe(Game) + " 的游戏目录", UseDescriptionForTitle = true };
        if (Directory.Exists(Root)) dialog.InitialDirectory = Root;
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        if (Games.ValidateRoot(Game, dialog.SelectedPath) is { } error) { ShowError(error); return; }
        rootBox.Text = Path.GetFullPath(dialog.SelectedPath);
        RefreshMods();
        RefreshPrerequisites();
        ShowPreview();
        SetStatus("已选择游戏目录");
    }

    private async Task BrowseArchiveAsync()
    {
        using var dialog = new OpenFileDialog { Filter = "压缩包 (*.zip;*.rar;*.7z)|*.zip;*.rar;*.7z", Title = "选择 Mod 压缩包" };
        if (dialog.ShowDialog(this) == DialogResult.OK) await OpenArchiveAsync(dialog.FileName);
    }

    private async Task OpenArchiveAsync(string path)
    {
        if (!new[] { ".zip", ".rar", ".7z" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
        { ShowError("只支持 ZIP、RAR、7Z 压缩包。"); return; }
        archivePath = path;
        archiveBox.Text = path;
        await AnalyzeAsync();
    }

    private async Task AnalyzeAsync()
    {
        if (archivePath is null) { ShowError("请先选择压缩包。"); return; }
        if (Games.ValidateRoot(Game, Root) is { } error) { ShowError(error); return; }
        await RunUiAsync(async () =>
        {
            analysis = await archives.AnalyzeAsync(archivePath, Game, NullIfBlank(mappingBox.Text));
            variantBox.Items.Clear();
            foreach (var variant in analysis.Variants) variantBox.Items.Add(variant.Name);
            if (variantBox.Items.Count == 1) variantBox.SelectedIndex = 0;
            ShowPreview();
            SetStatus($"分析完成：{analysis.Variants.Count} 个安装版本");
        });
    }

    private void ShowPreview()
    {
        if (analysis is null) return;
        var lines = new List<string> { "包：" + Path.GetFileName(analysis.ArchivePath), "SHA-256：" + analysis.Sha256 };
        lines.AddRange(analysis.Notes.Select(x => "提示：" + x));
        if (variantBox.SelectedIndex < 0)
        {
            lines.Add(analysis.Variants.Count > 1 ? "请选择一个安装版本。" : "没有可导入的文件。");
            previewBox.Lines = lines.ToArray();
            return;
        }
        var variant = analysis.Variants[variantBox.SelectedIndex];
        if (variant.BlockedReason is not null) lines.Add("阻止安装：" + variant.BlockedReason);
        if (variant.Requirements.Count > 0) lines.Add("前置：" + string.Join("；", variant.Requirements));
        lines.Add($"文件：{variant.Files.Count} 个");
        foreach (var file in variant.Files)
        {
            var target = Path.Combine(Root, file.Target.Replace('/', Path.DirectorySeparatorChar));
            var owner = manager.Mods.FirstOrDefault(m => m.Enabled && m.Game == Game &&
                m.GameRoot.Equals(Root, StringComparison.OrdinalIgnoreCase) &&
                m.Files.Any(f => f.Target.Equals(file.Target, StringComparison.OrdinalIgnoreCase)));
            var state = owner is not null ? " [与 " + owner.Name + " 冲突]" : File.Exists(target) ? " [将备份原文件]" : "";
            lines.Add(file.Source + "  →  " + target + state);
        }
        previewBox.Lines = lines.ToArray();
    }

    private async Task ImportAsync()
    {
        if (analysis is null || archivePath is null || variantBox.SelectedIndex < 0) { ShowError("请先分析压缩包并选择一个安装版本。"); return; }
        var variant = analysis.Variants[variantBox.SelectedIndex];
        if (variant.BlockedReason is not null) { ShowError(variant.BlockedReason); return; }
        await RunUiAsync(async () =>
        {
            var record = await manager.ImportAsync(archivePath, Game, Root, variant.Name, NullIfBlank(mappingBox.Text));
            RefreshMods();
            SetStatus($"已导入 {record.Name}；勾选后启用。");
        });
    }

    private async Task ToggleFromListAsync(ListViewItem item)
    {
        if (syncing || busy || item.Tag is not string id) return;
        var enabled = item.Checked;
        await RunUiAsync(async () =>
        {
            try { await manager.SetEnabledAsync(id, enabled); }
            catch (InvalidOperationException ex) when (enabled && ex.Message.StartsWith("目标文件与已启用 Mod 冲突：", StringComparison.Ordinal))
            {
                if (MessageBox.Show(this, ex.Message + "\n\n停用冲突 Mod 并启用当前 Mod？", "文件冲突", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                await ToggleCoreAsync(id, enabled, true);
            }
            catch (UnauthorizedAccessException) { await ToggleElevatedAsync(id, enabled, false); }
            manager.Reload();
            SetStatus((enabled ? "已启用：" : "已停用：") + item.Text);
        });
        RefreshMods();
        RefreshPrerequisites();
        ShowPreview();
    }

    private async Task ToggleCoreAsync(string id, bool enabled, bool switchConflicts)
    {
        try { await manager.SetEnabledAsync(id, enabled, switchConflicts); }
        catch (UnauthorizedAccessException) { await ToggleElevatedAsync(id, enabled, switchConflicts); }
    }

    private Task ToggleElevatedAsync(string id, bool enabled, bool switchConflicts)
    {
        if (!Program.RunElevated("--elevated-toggle", Program.DataRoot, id, enabled ? "enable" : "disable", switchConflicts ? "switch" : "normal"))
            throw new InvalidOperationException("管理员辅助进程未完成操作。");
        return Task.CompletedTask;
    }

    private void RefreshMods()
    {
        syncing = true;
        try
        {
            mods.BeginUpdate();
            mods.Items.Clear();
            foreach (var mod in manager.Mods.Where(x => x.Game == Game && x.GameRoot.Equals(Root, StringComparison.OrdinalIgnoreCase)))
            {
                var item = new ListViewItem(mod.Name) { Tag = mod.Id, Checked = mod.Enabled };
                item.SubItems.Add(mod.Enabled ? "已启用" : "已停用");
                item.ToolTipText = mod.Variant + " · " + mod.Files.Count + " 个文件";
                mods.Items.Add(item);
            }
            mods.EndUpdate();
        }
        finally { syncing = false; }
    }

    private void ShowSelectedMod()
    {
        if (mods.SelectedItems.Count == 0 || mods.SelectedItems[0].Tag is not string id) return;
        var mod = manager.Mods.FirstOrDefault(x => x.Id == id);
        if (mod is null) return;
        var lines = new List<string> { mod.Name, "版本：" + mod.Variant, "状态：" + (mod.Enabled ? "已启用" : "已停用"), "包哈希：" + mod.Sha256 };
        lines.AddRange(mod.Requirements.Select(x => "前置：" + x));
        lines.AddRange(mod.Files.Select(x => x.Source + "  →  " + x.Target));
        previewBox.Lines = lines.ToArray();
    }

    private void RemoveSelected()
    {
        if (mods.SelectedItems.Count == 0 || mods.SelectedItems[0].Tag is not string id) return;
        try
        {
            manager.Remove(id);
            RefreshMods();
            SetStatus("已移除导入记录。");
        }
        catch (Exception ex) { ShowError(ex.Message); }
    }

    private void RefreshPrerequisites()
    {
        prerequisitePanel.Controls.Clear();
        if (Games.ValidateRoot(Game, Root) is { } error)
        {
            prerequisitePanel.Controls.Add(new Label { Text = error, AutoSize = true, Padding = new Padding(8) });
            return;
        }
        foreach (var kind in Prerequisites.ForGame(Game))
        {
            var title = new Label { Text = Prerequisites.Display(kind), Font = new Font(Font, FontStyle.Bold), AutoSize = true, Margin = new Padding(8, 12, 8, 2) };
            var detail = new Label { Text = Prerequisites.Status(kind, Root), AutoSize = true, MaximumSize = new Size(560, 0), Margin = new Padding(8, 2, 8, 4) };
            prerequisitePanel.Controls.Add(title);
            prerequisitePanel.Controls.Add(detail);
            if (Prerequisites.IsReady(kind, Root)) continue;
            if (kind == PrerequisiteKind.WildsLooseFileLoader) continue;
            var button = new Button { AutoSize = true, Text = kind == PrerequisiteKind.REFramework ? "从官方 GitHub 下载并配置" : "导入已下载的压缩包…", Margin = new Padding(8, 2, 8, 8) };
            button.Click += async (_, _) => await ConfigurePrerequisiteAsync(kind);
            prerequisitePanel.Controls.Add(button);
        }
    }

    private async Task ConfigurePrerequisiteAsync(PrerequisiteKind kind)
    {
        string? path = null;
        var temporary = false;
        if (kind != PrerequisiteKind.REFramework)
        {
            using var dialog = new OpenFileDialog { Filter = "压缩包 (*.zip;*.rar;*.7z)|*.zip;*.rar;*.7z", Title = "选择 " + Prerequisites.Display(kind) + " 安装包" };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            path = dialog.FileName;
        }
        else
        {
            path = Path.Combine(Path.GetTempPath(), "hmm-reframework-" + Guid.NewGuid().ToString("N") + ".zip");
            temporary = true;
        }
        var selectedPath = path;
        await RunUiAsync(async () =>
        {
            try
            {
                if (kind == PrerequisiteKind.REFramework)
                {
                    SetStatus("正在从官方 GitHub 下载 REFramework…");
                    await Prerequisites.DownloadOfficialREFrameworkAsync(selectedPath);
                }
                var files = Prerequisites.SelectFiles(kind, await archives.ListFilesAsync(selectedPath));
                var preview = files.Select(file =>
                {
                    var target = Path.Combine(Root, file.Target.Replace('/', Path.DirectorySeparatorChar));
                    return target + (File.Exists(target) ? " [将备份原文件]" : " [新文件]");
                });
                var message = "仅部署以下文件：\n" + string.Join("\n", preview) + "\n\n继续导入并启用？";
                if (MessageBox.Show(this, message, Prerequisites.Display(kind), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                var record = await manager.ImportPrerequisiteAsync(selectedPath, kind, Game, Root);
                await ToggleCoreAsync(record.Id, true, false);
                manager.Reload();
                RefreshMods();
                RefreshPrerequisites();
                SetStatus("已部署前置组件；请启动游戏确认实际生效。");
            }
            finally { if (temporary && File.Exists(selectedPath)) File.Delete(selectedPath); }
        });
    }

    private async Task RunUiAsync(Func<Task> operation)
    {
        if (busy) return;
        busy = true;
        UseWaitCursor = true;
        try { await operation(); }
        catch (Exception ex) { ShowError(ex.Message); }
        finally { busy = false; UseWaitCursor = false; }
    }

    private void SetStatus(string message)
    {
        status.Text = message;
        logBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }

    private void ShowError(string message)
    {
        SetStatus("错误：" + message);
        MessageBox.Show(this, message, "Hunter Mod Manager", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private static string? NullIfBlank(string text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}

internal static class SteamLocator
{
    internal static string? Find(GameId game)
    {
        var steamRoots = new List<string>();
        var registryRoot = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")?.GetValue("SteamPath") as string;
        if (!string.IsNullOrWhiteSpace(registryRoot)) steamRoots.Add(registryRoot);
        steamRoots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
        foreach (var steam in steamRoots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var libraries = new List<string> { steam };
            var vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
            if (File.Exists(vdf))
            {
                foreach (Match match in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s*\"(?<path>(?:\\\\.|[^\"])*)\"", RegexOptions.IgnoreCase))
                    libraries.Add(match.Groups["path"].Value.Replace("\\\\", "\\"));
            }
            foreach (var library in libraries.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var root = Path.Combine(library, "steamapps", "common", Games.SteamFolder(game));
                if (Games.ValidateRoot(game, root) is null) return Path.GetFullPath(root);
            }
        }
        return null;
    }
}
