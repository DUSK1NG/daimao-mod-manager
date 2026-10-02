using System.Diagnostics;
using System.Text.Json;

namespace HunterModManager.Core;

public sealed class ModManager
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly ArchiveService archives;
    private readonly string dataRoot;
    private readonly string statePath;
    private readonly string journalPath;
    private readonly string lockPath;
    private InstalledState state;

    private sealed class Snapshot
    {
        public string Path { get; set; } = "";
        public bool Existed { get; set; }
        public string? Backup { get; set; }
        public string? OriginalHash { get; set; }
        public string? ExpectedHash { get; set; }
        public string TemporaryPath { get; set; } = "";
    }

    private sealed class Journal
    {
        public string BeforeState { get; set; } = "";
        public List<Snapshot> Files { get; set; } = [];
        public string WorkDirectory { get; set; } = "";
    }

    public ModManager(string dataRoot, ArchiveService archives)
    {
        this.dataRoot = Path.GetFullPath(dataRoot);
        this.archives = archives;
        Directory.CreateDirectory(this.dataRoot);
        statePath = Path.Combine(this.dataRoot, "state.json");
        journalPath = Path.Combine(this.dataRoot, "pending-operation.json");
        lockPath = Path.Combine(this.dataRoot, "operation.lock");
        using var processLock = AcquireLock();
        RecoverCore();
        state = ReadState();
    }

    public IReadOnlyList<ModRecord> Mods => state.Mods.AsReadOnly();

    public void Reload()
    {
        using var processLock = AcquireLock();
        RecoverCore();
        state = ReadState();
    }

    public async Task<ModRecord> ImportAsync(string archivePath, GameId game, string gameRoot, string? variantName = null, string? manualTarget = null, CancellationToken ct = default)
    {
        var rootError = Games.ValidateRoot(game, gameRoot);
        if (rootError is not null) throw new InvalidDataException(rootError);
        var analysis = await archives.AnalyzeAsync(archivePath, game, manualTarget, ct);
        var variant = variantName is null ? (analysis.Variants.Count == 1 ? analysis.Variants[0] : null) : analysis.Variants.SingleOrDefault(x => x.Name.Equals(variantName, StringComparison.OrdinalIgnoreCase));
        if (variant is null) throw new InvalidDataException(analysis.Variants.Count == 0 ? string.Join("；", analysis.Notes) : "请选择一个安装版本");
        if (variant.BlockedReason is not null) throw new InvalidDataException(variant.BlockedReason);
        var extension = Path.GetExtension(archivePath).ToLowerInvariant();
        var stored = Path.Combine(dataRoot, "packages", analysis.Sha256 + extension);
        Directory.CreateDirectory(Path.GetDirectoryName(stored)!);
        if (!File.Exists(stored)) File.Copy(archivePath, stored);
        if (!analysis.Sha256.Equals(ArchiveService.HashFile(stored), StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("压缩包复制后校验失败");
        var record = new ModRecord
        {
            Name = Path.GetFileNameWithoutExtension(archivePath), Game = game, GameRoot = Path.GetFullPath(gameRoot),
            ArchivePath = stored, Sha256 = analysis.Sha256, Variant = variant.Name, ManualTarget = manualTarget,
            Files = variant.Files, Requirements = variant.Requirements, BlockedReason = variant.BlockedReason
        };
        await gate.WaitAsync(ct);
        try
        {
            using var processLock = await AcquireLockAsync(ct);
            RecoverCore();
            state = ReadState();
            state.Mods.Add(record);
            SaveState();
        }
        finally { gate.Release(); }
        return record;
    }

    public async Task<ModRecord> ImportPrerequisiteAsync(string archivePath, PrerequisiteKind kind, GameId game, string gameRoot, CancellationToken ct = default)
    {
        if (Games.ValidateRoot(game, gameRoot) is { } error) throw new InvalidDataException(error);
        if (!Prerequisites.ForGame(game).Contains(kind) || kind == PrerequisiteKind.WildsLooseFileLoader) throw new InvalidDataException("该前置组件不适用于所选游戏");
        var files = Prerequisites.SelectFiles(kind, await archives.ListFilesAsync(archivePath, ct));
        var hash = ArchiveService.HashFile(archivePath);
        var stored = Path.Combine(dataRoot, "packages", hash + Path.GetExtension(archivePath).ToLowerInvariant());
        Directory.CreateDirectory(Path.GetDirectoryName(stored)!);
        if (!File.Exists(stored)) File.Copy(archivePath, stored);
        if (!hash.Equals(ArchiveService.HashFile(stored), StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("前置包复制后校验失败");
        var record = new ModRecord
        {
            Name = "[前置] " + Prerequisites.Display(kind), IsPrerequisite = true, Prerequisite = kind,
            Game = game, GameRoot = Path.GetFullPath(gameRoot), ArchivePath = stored,
            Sha256 = hash, Files = files, Variant = "前置环境"
        };
        await gate.WaitAsync(ct);
        try
        {
            using var processLock = await AcquireLockAsync(ct);
            RecoverCore();
            state = ReadState();
            state.Mods.Add(record);
            SaveState();
        }
        finally { gate.Release(); }
        return record;
    }

    public void Remove(string modId)
    {
        gate.Wait();
        try
        {
            using var processLock = AcquireLock();
            RecoverCore();
            state = ReadState();
            var mod = Find(modId);
            if (mod.Enabled) throw new InvalidOperationException("请先停用 Mod");
            state.Mods.Remove(mod);
            SaveState();
        }
        finally { gate.Release(); }
    }

    public async Task SetEnabledAsync(string modId, bool enabled, bool switchConflicts = false, CancellationToken ct = default)
    {
        await gate.WaitAsync(ct);
        try
        {
            using var processLock = await AcquireLockAsync(ct);
            RecoverCore();
            state = ReadState();
            var mod = Find(modId);
            if (mod.Enabled == enabled) return;
            if (Games.ValidateRoot(mod.Game, mod.GameRoot) is { } error) throw new InvalidDataException(error);
            if (enabled && !Prerequisites.RequirementsMet(mod)) throw new InvalidOperationException("所需前置环境尚未配置，请先打开“前置环境”");
            if (!enabled && mod.IsPrerequisite && state.Mods.Any(m => m.Enabled && !m.IsPrerequisite && m.Game == mod.Game && SamePath(m.GameRoot, mod.GameRoot) && Requires(m, mod.Prerequisite)))
                throw new InvalidOperationException("仍有启用的 Mod 需要前置环境，请先停用它们");
            var gameProcesses = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(Games.Exe(mod.Game)));
            var gameRunning = gameProcesses.Length > 0;
            foreach (var process in gameProcesses) process.Dispose();
            if (gameRunning) throw new InvalidOperationException("请先关闭游戏，再启用或停用 Mod");
            ValidateTargets(mod, enabled);
            var modTargets = mod.Files.ToDictionary(f => TargetPath(mod, f.Target), StringComparer.OrdinalIgnoreCase);
            var conflicts = enabled ? state.Mods.Where(m => m.Enabled && m.Game == mod.Game && SamePath(m.GameRoot, mod.GameRoot) &&
                m.Files.Any(f => modTargets.ContainsKey(TargetPath(m, f.Target)))).ToList() : [];
            if (conflicts.Count > 0 && !switchConflicts) throw new InvalidOperationException("目标文件与已启用 Mod 冲突：" + string.Join("、", conflicts.Select(x => x.Name)));
            foreach (var conflict in conflicts) ValidateTargets(conflict, false);
            var changed = conflicts.Append(mod).ToList();
            foreach (var old in changed.Where(x => x.Enabled)) CheckCurrentFiles(old);
            var targets = changed.SelectMany(x => x.Files.Select(f => TargetPath(x, f.Target))).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var path in targets)
            {
                if (state.Mods.Any(m => m.Enabled && m.Files.Any(f => SamePath(TargetPath(m, f.Target), path)))) continue;
                var baseline = state.Baselines.SingleOrDefault(x => SamePath(x.Path, path));
                if (baseline is null) continue;
                if (baseline.Existed ? !File.Exists(path) || baseline.Sha256 != ArchiveService.HashFile(path) : File.Exists(path))
                    throw new InvalidOperationException($"文件已被外部修改，停止操作：{path}");
            }
            var drive = new DriveInfo(Path.GetPathRoot(mod.GameRoot)!);
            var required = changed.SelectMany(x => x.Files).Sum(x => Math.Max(0, x.Size));
            if (drive.AvailableFreeSpace < required * 2 + 64L * 1024 * 1024) throw new IOException("目标磁盘剩余空间不足以安全备份和安装");
            var work = Path.Combine(dataRoot, "transactions", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(work);
            var journal = new Journal { BeforeState = JsonSerializer.Serialize(state, JsonSettings.Options), WorkDirectory = work };
            try
            {
                var staged = new Dictionary<string, string>();
                if (enabled)
                {
                    var unpack = Path.Combine(work, "unpack-" + mod.Id);
                    await archives.ExtractAsync(mod.ArchivePath, unpack, CancellationToken.None);
                    staged[mod.Id] = unpack;
                }
                for (var i = 0; i < targets.Count; i++)
                {
                    var path = targets[i];
                    var snapshot = new Snapshot { Path = path, Existed = File.Exists(path), TemporaryPath = path + ".hmm-" + Path.GetFileName(work) + ".tmp" };
                    if (snapshot.Existed)
                    {
                        snapshot.Backup = Path.Combine(work, i.ToString("D5") + ".bak");
                        File.Copy(path, snapshot.Backup);
                        snapshot.OriginalHash = ArchiveService.HashFile(path);
                    }
                    if (state.Baselines.All(x => !SamePath(x.Path, path)))
                    {
                        var baseline = new BaselineRecord { Path = path, Existed = snapshot.Existed };
                        if (snapshot.Existed)
                        {
                            baseline.Sha256 = snapshot.OriginalHash;
                            baseline.BackupPath = Path.Combine(dataRoot, "baselines", Guid.NewGuid().ToString("N") + ".bak");
                            Directory.CreateDirectory(Path.GetDirectoryName(baseline.BackupPath)!);
                            File.Copy(path, baseline.BackupPath);
                        }
                        state.Baselines.Add(baseline);
                    }
                    if (enabled && modTargets.TryGetValue(path, out var file))
                    {
                        snapshot.ExpectedHash = ArchiveService.HashFile(SafeJoin(staged[mod.Id], file.Source));
                    }
                    else
                    {
                        snapshot.ExpectedHash = state.Baselines.Single(x => SamePath(x.Path, path)).Sha256;
                    }
                    journal.Files.Add(snapshot);
                }
                WriteAtomic(journalPath, JsonSerializer.Serialize(journal, JsonSettings.Options));
                foreach (var conflict in conflicts) conflict.Enabled = false;
                mod.Enabled = enabled;
                foreach (var path in targets)
                {
                    var winner = state.Mods.FirstOrDefault(m => m.Enabled && m.Files.Any(f => SamePath(TargetPath(m, f.Target), path)));
                    if (winner is not null)
                    {
                        if (!staged.TryGetValue(winner.Id, out var unpack))
                        {
                            if (!winner.Sha256.Equals(ArchiveService.HashFile(winner.ArchivePath), StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("导入的压缩包已被外部修改");
                            unpack = Path.Combine(work, "unpack-" + winner.Id);
                            await archives.ExtractAsync(winner.ArchivePath, unpack, CancellationToken.None);
                            staged[winner.Id] = unpack;
                        }
                        var file = winner.Files.Single(f => SamePath(TargetPath(winner, f.Target), path));
                        var source = SafeJoin(unpack, file.Source);
                        if (!File.Exists(source)) throw new InvalidDataException($"压缩包缺少文件：{file.Source}");
                        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                        CopyAtomic(source, path, journal.Files.Single(x => SamePath(x.Path, path)).TemporaryPath);
                        file.InstalledHash = ArchiveService.HashFile(source);
                        if (!file.InstalledHash.Equals(ArchiveService.HashFile(path), StringComparison.OrdinalIgnoreCase)) throw new IOException("部署文件校验失败");
                    }
                    else
                    {
                        var baseline = state.Baselines.Single(x => SamePath(x.Path, path));
                        if (baseline.Existed)
                        {
                            if (baseline.BackupPath is null || !File.Exists(baseline.BackupPath)) throw new IOException("原文件备份丢失，不能停用");
                            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                            CopyAtomic(baseline.BackupPath, path, journal.Files.Single(x => SamePath(x.Path, path)).TemporaryPath);
                            if (baseline.Sha256 != ArchiveService.HashFile(path)) throw new IOException("原文件恢复校验失败");
                        }
                        else if (File.Exists(path)) File.Delete(path);
                    }
                }
                foreach (var inactive in changed.Where(x => !x.Enabled)) foreach (var file in inactive.Files) file.InstalledHash = null;
                SaveState();
                File.Delete(journalPath);
                try { Directory.Delete(work, true); } catch (IOException) { /* 已提交，残留临时文件可稍后清理 */ }
                catch (UnauthorizedAccessException) { /* 已提交，残留临时文件可稍后清理 */ }
            }
            catch
            {
                if (File.Exists(journalPath)) RecoverCore();
                else if (Directory.Exists(work)) Directory.Delete(work, true);
                state = ReadState();
                throw;
            }
        }
        finally { gate.Release(); }
    }

    private void ValidateTargets(ModRecord mod, bool requireArchive)
    {
        if (requireArchive && (!File.Exists(mod.ArchivePath) || !mod.Sha256.Equals(ArchiveService.HashFile(mod.ArchivePath), StringComparison.OrdinalIgnoreCase))) throw new InvalidDataException("压缩包缓存已被修改");
        if (mod.Files.Count == 0 || mod.Files.Any(f => !(mod.IsPrerequisite ? Prerequisites.IsAllowedTarget(mod.Game, ArchiveService.CleanRelative(f.Target)) : ArchiveService.IsAllowedTarget(mod.Game, ArchiveService.CleanRelative(f.Target))))) throw new InvalidDataException("Mod 包含不允许的目标路径");
        foreach (var file in mod.Files) _ = TargetPath(mod, file.Target);
    }

    private void CheckCurrentFiles(ModRecord mod)
    {
        foreach (var file in mod.Files)
        {
            var path = TargetPath(mod, file.Target);
            if (file.InstalledHash is null || !File.Exists(path) || !file.InstalledHash.Equals(ArchiveService.HashFile(path), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"文件已被外部修改，停止操作：{path}");
        }
    }

    private static string TargetPath(ModRecord mod, string relative) => SafeJoin(mod.GameRoot, relative);

    private static string SafeJoin(string root, string relative)
    {
        relative = ArchiveService.CleanRelative(relative);
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(fullRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("目标路径越界");
        var current = fullRoot.TrimEnd(Path.DirectorySeparatorChar);
        if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("游戏目录是链接，不能安全写入");
        foreach (var part in relative.Split('/'))
        {
            current = Path.Combine(current, part);
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("目标路径包含链接，不能安全写入");
        }
        return full;
    }

    private static bool SamePath(string a, string b) => Path.GetFullPath(a).Equals(Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
    private static bool Requires(ModRecord mod, PrerequisiteKind? kind) => kind switch
    {
        PrerequisiteKind.StrackersLoader => mod.Requirements.Any(x => x.Contains("Stracker", StringComparison.OrdinalIgnoreCase)),
        PrerequisiteKind.REFramework => mod.Requirements.Any(x => x.Contains("REFramework", StringComparison.OrdinalIgnoreCase)),
        PrerequisiteKind.FirstNatives => mod.Requirements.Any(x => x.Contains("FirstNatives", StringComparison.OrdinalIgnoreCase)),
        _ => false
    };
    private ModRecord Find(string id) => state.Mods.SingleOrDefault(x => x.Id == id) ?? throw new InvalidDataException("找不到 Mod");
    private InstalledState ReadState() => File.Exists(statePath) ? JsonSerializer.Deserialize<InstalledState>(File.ReadAllText(statePath), JsonSettings.Options) ?? new InstalledState() : new InstalledState();
    private void SaveState() => WriteAtomic(statePath, JsonSerializer.Serialize(state, JsonSettings.Options));

    private static void WriteAtomic(string path, string contents)
    {
        var temp = path + ".new";
        File.WriteAllText(temp, contents);
        File.Move(temp, path, true);
    }

    public void RecoverIfNeeded()
    {
        using var processLock = AcquireLock();
        RecoverCore();
    }

    private void RecoverCore()
    {
        if (!File.Exists(journalPath)) return;
        var journal = JsonSerializer.Deserialize<Journal>(File.ReadAllText(journalPath), JsonSettings.Options) ?? throw new InvalidDataException("恢复日志损坏");
        ValidateRecoveryJournal(journal);
        foreach (var file in journal.Files.AsEnumerable().Reverse())
        {
            var currentHash = File.Exists(file.Path) ? ArchiveService.HashFile(file.Path) : null;
            if (!string.Equals(currentHash, file.OriginalHash, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(currentHash, file.ExpectedHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("恢复期间发现目标文件被外部修改，请先手动处理：" + file.Path);
            if (file.Existed)
            {
                if (file.Backup is null || !File.Exists(file.Backup)) throw new IOException("恢复所需备份丢失：" + file.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(file.Path)!);
                CopyAtomic(file.Backup, file.Path, file.TemporaryPath);
            }
            else if (File.Exists(file.Path)) File.Delete(file.Path);
            if (File.Exists(file.TemporaryPath)) File.Delete(file.TemporaryPath);
        }
        WriteAtomic(statePath, journal.BeforeState);
        File.Delete(journalPath);
        if (Directory.Exists(journal.WorkDirectory)) Directory.Delete(journal.WorkDirectory, true);
    }

    private FileStream AcquireLock()
    {
        for (var attempt = 0; attempt < 150; attempt++)
        {
            try { return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) when (attempt < 149) { Thread.Sleep(200); }
        }
        throw new IOException("无法获取 Mod 管理器跨进程锁");
    }

    private async Task<FileStream> AcquireLockAsync(CancellationToken ct)
    {
        for (var attempt = 0; attempt < 150; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try { return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) when (attempt < 149) { await Task.Delay(200, ct); }
        }
        throw new IOException("无法获取 Mod 管理器跨进程锁");
    }

    private void ValidateRecoveryJournal(Journal journal)
    {
        var transactionsRoot = Path.GetFullPath(Path.Combine(dataRoot, "transactions")).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var work = Path.GetFullPath(journal.WorkDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!work.StartsWith(transactionsRoot, StringComparison.OrdinalIgnoreCase) || work == transactionsRoot || !Directory.Exists(work))
            throw new InvalidDataException("恢复日志的工作目录无效");
        if ((File.GetAttributes(work.TrimEnd(Path.DirectorySeparatorChar)) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("恢复目录不能是链接");
        var before = JsonSerializer.Deserialize<InstalledState>(journal.BeforeState, JsonSettings.Options) ?? throw new InvalidDataException("恢复日志的状态无效");
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mod in before.Mods)
        {
            if (Games.ValidateRoot(mod.Game, mod.GameRoot) is not null) throw new InvalidDataException("恢复日志中的游戏目录无效");
            foreach (var file in mod.Files)
            {
                var relative = ArchiveService.CleanRelative(file.Target);
                if (!(mod.IsPrerequisite ? Prerequisites.IsAllowedTarget(mod.Game, relative) : ArchiveService.IsAllowedTarget(mod.Game, relative)))
                    throw new InvalidDataException("恢复日志中的目标目录无效");
                allowed.Add(TargetPath(mod, relative));
            }
        }
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in journal.Files)
        {
            var path = Path.GetFullPath(file.Path);
            if (!allowed.Contains(path) || !seen.Add(path)) throw new InvalidDataException("恢复日志包含未经授权或重复的目标文件");
            if (!SamePath(file.TemporaryPath, file.Path + ".hmm-" + Path.GetFileName(work.TrimEnd(Path.DirectorySeparatorChar)) + ".tmp"))
                throw new InvalidDataException("恢复日志中的临时文件路径无效");
            if (file.Existed && !IsSha256(file.OriginalHash) || file.ExpectedHash is not null && !IsSha256(file.ExpectedHash))
                throw new InvalidDataException("恢复日志中的文件哈希无效");
            if (file.Existed)
            {
                if (file.Backup is null || !Path.GetDirectoryName(Path.GetFullPath(file.Backup))!.TrimEnd(Path.DirectorySeparatorChar).Equals(work.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase) ||
                    !File.Exists(file.Backup) || (File.GetAttributes(file.Backup) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("恢复日志中的备份文件无效");
                if (!string.Equals(ArchiveService.HashFile(file.Backup), file.OriginalHash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("恢复备份已被修改，停止自动恢复：" + file.Path);
            }
        }
    }

    private static bool IsSha256(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private static void CopyAtomic(string source, string target, string temporaryPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(source, temporaryPath, true);
        File.Move(temporaryPath, target, true);
    }
}
