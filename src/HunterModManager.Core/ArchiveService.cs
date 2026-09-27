using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace HunterModManager.Core;

public sealed class ArchiveService(string sevenZipPath)
{
    private static readonly HashSet<string> ArchiveExtensions = new(StringComparer.OrdinalIgnoreCase) { ".zip", ".rar", ".7z" };
    private static readonly HashSet<string> Documents = new(StringComparer.OrdinalIgnoreCase) { ".txt", ".md", ".png", ".jpg", ".jpeg", ".webp", ".pdf", ".url" };
    private static readonly HashSet<string> Executables = new(StringComparer.OrdinalIgnoreCase) { ".exe", ".bat", ".cmd", ".ps1", ".vbs", ".js", ".msi", ".lnk" };
    private static readonly Regex UnixMode = new(@"\b([0-9A-Fa-f]{8})$", RegexOptions.Compiled);

    public string SevenZipPath { get; } = File.Exists(sevenZipPath) ? Path.GetFullPath(sevenZipPath) : throw new FileNotFoundException("找不到 7-Zip", sevenZipPath);

    public static string HashFile(string path)
    {
        using var input = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(input));
    }

    public static string CleanRelative(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.StartsWith('\\') || path.StartsWith('/') || Path.IsPathRooted(path)) throw new InvalidDataException("压缩包含绝对路径");
        var parts = path.Replace('\\', '/').Split('/');
        if (parts.Any(part => part.Length == 0 || part is "." or ".." || part.Contains(':') || part.EndsWith(' ') || part.EndsWith('.') || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
            throw new InvalidDataException($"压缩包含不安全路径：{path}");
        foreach (var part in parts)
        {
            var stem = part.Split('.')[0];
            if (new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" }.Contains(stem, StringComparer.OrdinalIgnoreCase))
                throw new InvalidDataException($"压缩包含 Windows 保留名称：{path}");
        }
        return string.Join('/', parts);
    }

    private sealed class Entry
    {
        public string Path { get; set; } = "";
        public long Size { get; set; }
        public bool Folder { get; set; }
        public bool Encrypted { get; set; }
        public string Attributes { get; set; } = "";
    }

    public async Task<PackageAnalysis> AnalyzeAsync(string archivePath, GameId game, string? manualTarget = null, CancellationToken ct = default)
    {
        archivePath = Path.GetFullPath(archivePath);
        var entries = await ReadEntriesAsync(archivePath, ct);
        var result = new PackageAnalysis { ArchivePath = archivePath, Sha256 = HashFile(archivePath) };
        var groups = new Dictionary<string, List<PackageFile>>(StringComparer.OrdinalIgnoreCase);
        var hasPak = false;
        var hasInstaller = false;
        var unmapped = false;
        foreach (var entry in entries.Where(e => !e.Folder))
        {
            var ext = Path.GetExtension(entry.Path);
            if (ext.Equals(".pak", StringComparison.OrdinalIgnoreCase)) { hasPak = true; continue; }
            if (Executables.Contains(ext)) { hasInstaller = true; continue; }
            var pieces = entry.Path.Split('/');
            var anchor = Array.FindIndex(pieces, x => x.Equals("nativePC", StringComparison.OrdinalIgnoreCase) || x.Equals("natives", StringComparison.OrdinalIgnoreCase) || x.Equals("reframework", StringComparison.OrdinalIgnoreCase));
            string? target = null;
            string prefix = "默认";
            if (anchor >= 0)
            {
                prefix = anchor == 0 ? "默认" : string.Join('/', pieces[..anchor]);
                target = string.Join('/', pieces[anchor..]);
            }
            else if (!string.IsNullOrWhiteSpace(manualTarget) && !(pieces.Length == 1 && Documents.Contains(ext)))
            {
                target = CleanRelative(manualTarget.TrimEnd('/', '\\') + "/" + entry.Path);
            }
            if (target is null)
            {
                if (!Documents.Contains(ext)) unmapped = true;
                continue;
            }
            if (!IsAllowedTarget(game, target)) { unmapped = true; continue; }
            if (!groups.TryGetValue(prefix, out var files)) groups[prefix] = files = [];
            files.Add(new PackageFile { Source = entry.Path, Target = target, Size = entry.Size });
        }
        foreach (var group in groups.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            var variant = new PackageVariant { Name = group.Key, Files = group.Value };
            if (variant.Files.Select(x => x.Target).Distinct(StringComparer.OrdinalIgnoreCase).Count() != variant.Files.Count) variant.BlockedReason = "选项内存在重复目标文件";
            if (hasPak) variant.BlockedReason = "压缩包还包含 PAK；首版不能只安装其中一部分";
            if (hasInstaller) variant.BlockedReason = "压缩包还包含安装程序或脚本；请按作者说明处理";
            if (unmapped) variant.BlockedReason = "压缩包有未映射的文件；请选择目标目录或按作者说明处理，不能只安装其中一部分";
            if (variant.Files.Any(x => x.Target.StartsWith("natives/", StringComparison.OrdinalIgnoreCase)))
                variant.Requirements.Add(game == GameId.Rise ? "REFramework + FirstNatives" : "REFramework + Loose File Loader");
            if (variant.Files.Any(x => x.Target.StartsWith("reframework/", StringComparison.OrdinalIgnoreCase))) variant.Requirements.Add("REFramework");
            if (game == GameId.World && variant.Files.Any(x => x.Target.StartsWith("nativePC/", StringComparison.OrdinalIgnoreCase))) variant.Requirements.Add("Stracker's Loader（按 Mod 需求检查）");
            result.Variants.Add(variant);
        }
        if (hasPak) result.Notes.Add("检测到 PAK 文件；首版暂不自动安装或启停 PAK");
        if (hasInstaller) result.Notes.Add("检测到独立安装器或脚本；工具不会执行它们");
        if (unmapped) result.Notes.Add("有文件未映射到可识别的游戏目录，已阻止部分安装");
        if (result.Variants.Count == 0)
            result.Notes.Add("未识别到可自动安装的游戏目录结构；可选择手动目标目录后重新预览");
        return result;
    }

    public async Task<IReadOnlyList<PackageFile>> ListFilesAsync(string archivePath, CancellationToken ct = default)
    {
        var entries = await ReadEntriesAsync(Path.GetFullPath(archivePath), ct);
        return entries.Where(x => !x.Folder).Select(x => new PackageFile { Source = x.Path, Target = x.Path, Size = x.Size }).ToList();
    }

    private async Task<List<Entry>> ReadEntriesAsync(string archivePath, CancellationToken ct)
    {
        if (!File.Exists(archivePath) || !ArchiveExtensions.Contains(Path.GetExtension(archivePath))) throw new InvalidDataException("只支持 ZIP、RAR、7Z 文件");
        var output = await RunAsync(["l", "-slt", "-sccUTF-8", archivePath], ct);
        var entries = ParseListing(output);
        if (entries.Count == 0) throw new InvalidDataException("压缩包没有文件");
        if (entries.Count > 100_000) throw new InvalidDataException("压缩包条目超过 100000 个，暂不支持自动安装");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        foreach (var entry in entries)
        {
            entry.Path = CleanRelative(entry.Path);
            if (!names.Add(entry.Path)) throw new InvalidDataException($"压缩包含大小写重名路径：{entry.Path}");
            if (entry.Encrypted) throw new InvalidDataException("加密压缩包暂不支持");
            if (IsLink(entry.Attributes)) throw new InvalidDataException($"压缩包含链接：{entry.Path}");
            total = checked(total + entry.Size);
        }
        if (total > 16L * 1024 * 1024 * 1024) throw new InvalidDataException("解压后超过 16 GiB，暂不支持自动安装");
        return entries;
    }

    public static bool IsAllowedTarget(GameId game, string target)
    {
        var parts = target.Split('/');
        if (game == GameId.World) return parts.Length > 1 && parts[0].Equals("nativePC", StringComparison.OrdinalIgnoreCase);
        if (parts.Length < 2) return false;
        if (parts[0].Equals("natives", StringComparison.OrdinalIgnoreCase)) return parts.Length > 2 && parts[1].Equals("STM", StringComparison.OrdinalIgnoreCase);
        if (parts[0].Equals("reframework", StringComparison.OrdinalIgnoreCase)) return parts.Length > 2 && new[] { "autorun", "plugins", "data" }.Contains(parts[1], StringComparer.OrdinalIgnoreCase);
        return false;
    }

    private static bool IsLink(string attributes)
    {
        if (attributes.TrimStart().StartsWith('l')) return true; // 7-Zip ZIP/7Z Unix 权限列，例如 lrwxrwxrwx。
        var match = UnixMode.Match(attributes);
        if (match.Success && uint.TryParse(match.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
            return ((value >> 16) & 0xF000) == 0xA000;
        return attributes.Contains('L');
    }

    private static List<Entry> ParseListing(string output)
    {
        var delimiter = output.IndexOf("----------", StringComparison.Ordinal);
        if (delimiter < 0) throw new InvalidDataException("无法读取压缩包目录");
        var entries = new List<Entry>();
        Entry? entry = null;
        foreach (var line in output[(delimiter + 10)..].Split('\n'))
        {
            var trimmed = line.TrimEnd('\r');
            if (trimmed.StartsWith("Path = ", StringComparison.Ordinal))
            {
                if (entry is not null) entries.Add(entry);
                entry = new Entry { Path = trimmed[7..] };
            }
            else if (entry is not null)
            {
                if (trimmed.StartsWith("Size = ", StringComparison.Ordinal) && long.TryParse(trimmed[7..], out var size)) entry.Size = size;
                else if (trimmed == "Folder = +") entry.Folder = true;
                else if (trimmed == "Encrypted = +") entry.Encrypted = true;
                else if (trimmed.StartsWith("Attributes = ", StringComparison.Ordinal)) entry.Attributes = trimmed[13..];
            }
        }
        if (entry is not null) entries.Add(entry);
        return entries;
    }

    public async Task ExtractAsync(string archivePath, string destination, CancellationToken ct = default)
    {
        var entries = await ReadEntriesAsync(Path.GetFullPath(archivePath), ct);
        var required = entries.Sum(x => x.Size);
        var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(destination))!);
        if (drive.AvailableFreeSpace < required + 64L * 1024 * 1024)
            throw new IOException("临时解压目录的磁盘剩余空间不足");
        Directory.CreateDirectory(destination);
        await RunAsync(["x", "-y", "-sccUTF-8", "-bso0", "-bsp0", $"-o{destination}", archivePath], ct);
        var fullRoot = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        foreach (var item in Directory.EnumerateFileSystemEntries(destination, "*", SearchOption.AllDirectories))
        {
            var full = Path.GetFullPath(item);
            if (!full.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) || (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("解压后发现越界路径或链接");
        }
    }

    private async Task<string> RunAsync(IEnumerable<string> args, CancellationToken ct)
    {
        var info = new ProcessStartInfo(SevenZipPath) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        using var process = Process.Start(info) ?? throw new IOException("无法启动 7-Zip");
        var stdout = process.StandardOutput.ReadToEndAsync(ct);
        var stderr = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        var result = await stdout;
        var error = await stderr;
        if (process.ExitCode != 0) throw new InvalidDataException($"7-Zip 失败（{process.ExitCode}）：{error.Trim()}");
        return result;
    }
}
