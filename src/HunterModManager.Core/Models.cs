using System.Text.Json;

namespace HunterModManager.Core;

public enum GameId { World, Rise, Wilds }

public static class Games
{
    public static string Display(GameId game) => game switch
    {
        GameId.World => "怪物猎人：世界",
        GameId.Rise => "怪物猎人：崛起",
        _ => "怪物猎人：荒野"
    };

    public static string Exe(GameId game) => game switch
    {
        GameId.World => "MonsterHunterWorld.exe",
        GameId.Rise => "MonsterHunterRise.exe",
        _ => "MonsterHunterWilds.exe"
    };

    public static string SteamFolder(GameId game) => game switch
    {
        GameId.World => "Monster Hunter World",
        GameId.Rise => "MonsterHunterRise",
        _ => "MonsterHunterWilds"
    };

    public static string? ValidateRoot(GameId game, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return "游戏目录不存在";
        return File.Exists(Path.Combine(path, Exe(game))) ? null : $"未找到 {Exe(game)}";
    }
}

public sealed class PackageFile
{
    public string Source { get; set; } = "";
    public string Target { get; set; } = "";
    public long Size { get; set; }
    public string? InstalledHash { get; set; }
}

public sealed class PackageVariant
{
    public string Name { get; set; } = "默认";
    public List<PackageFile> Files { get; set; } = [];
    public List<string> Requirements { get; set; } = [];
    public string? BlockedReason { get; set; }
}

public sealed class PackageAnalysis
{
    public string ArchivePath { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public List<PackageVariant> Variants { get; set; } = [];
    public List<string> Notes { get; set; } = [];
}

public sealed class ModRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public bool IsPrerequisite { get; set; }
    public PrerequisiteKind? Prerequisite { get; set; }
    public GameId Game { get; set; }
    public string GameRoot { get; set; } = "";
    public string ArchivePath { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public string Variant { get; set; } = "默认";
    public string? ManualTarget { get; set; }
    public List<PackageFile> Files { get; set; } = [];
    public List<string> Requirements { get; set; } = [];
    public bool Enabled { get; set; }
    public string? BlockedReason { get; set; }
}

public sealed class BaselineRecord
{
    public string Path { get; set; } = "";
    public bool Existed { get; set; }
    public string? BackupPath { get; set; }
    public string? Sha256 { get; set; }
}

public sealed class InstalledState
{
    public List<ModRecord> Mods { get; set; } = [];
    public List<BaselineRecord> Baselines { get; set; } = [];
}

public static class JsonSettings
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };
}
