using System.Net.Http;

namespace HunterModManager.Core;

public enum PrerequisiteKind { StrackersLoader, REFramework, FirstNatives, WildsLooseFileLoader }

public static class Prerequisites
{
    private const string REFrameworkUrl = "https://github.com/praydog/REFramework-nightly/releases/latest/download/REFramework.zip";

    public static string Display(PrerequisiteKind kind) => kind switch
    {
        PrerequisiteKind.StrackersLoader => "Stracker's Loader",
        PrerequisiteKind.REFramework => "REFramework",
        PrerequisiteKind.FirstNatives => "FirstNatives",
        _ => "Wilds Loose File Loader"
    };

    public static PrerequisiteKind[] ForGame(GameId game) => game switch
    {
        GameId.World => [PrerequisiteKind.StrackersLoader],
        GameId.Rise => [PrerequisiteKind.REFramework, PrerequisiteKind.FirstNatives],
        _ => [PrerequisiteKind.REFramework, PrerequisiteKind.WildsLooseFileLoader]
    };

    public static bool IsReady(PrerequisiteKind kind, string root) => kind switch
    {
        PrerequisiteKind.StrackersLoader => File.Exists(Path.Combine(root, "dinput8.dll")) && File.Exists(Path.Combine(root, "loader.dll")),
        PrerequisiteKind.REFramework => File.Exists(Path.Combine(root, "dinput8.dll")),
        PrerequisiteKind.FirstNatives => File.Exists(Path.Combine(root, "reframework", "plugins", "FirstNatives.dll")),
        _ => File.Exists(Path.Combine(root, "dinput8.dll")) && IsLooseFileLoaderEnabled(root)
    };

    public static string Status(PrerequisiteKind kind, string root)
    {
        if (IsReady(kind, root)) return "已发现所需文件/设置；游戏内生效仍需启动验证";
        return kind switch
        {
            PrerequisiteKind.REFramework => "可从 praydog 官方 GitHub 下载并配置",
            PrerequisiteKind.WildsLooseFileLoader => "需在 REFramework 菜单启用 Loose File Loader，关闭菜单保存设置并重启游戏",
            _ => "需导入作者发布的压缩包"
        };
    }

    public static bool RequirementsMet(ModRecord mod)
    {
        if (mod.IsPrerequisite) return true;
        if (mod.Game == GameId.World) return true; // nativePC 文件是否需要 Loader 取决于具体 Mod，UI 仍显示提醒。
        var needsRef = mod.Requirements.Any(x => x.Contains("REFramework", StringComparison.OrdinalIgnoreCase));
        if (needsRef && !IsReady(PrerequisiteKind.REFramework, mod.GameRoot)) return false;
        if (mod.Game == GameId.Rise && mod.Files.Any(x => x.Target.StartsWith("natives/", StringComparison.OrdinalIgnoreCase)))
            return IsReady(PrerequisiteKind.FirstNatives, mod.GameRoot);
        if (mod.Game == GameId.Wilds && mod.Files.Any(x => x.Target.StartsWith("natives/", StringComparison.OrdinalIgnoreCase)))
            return IsReady(PrerequisiteKind.WildsLooseFileLoader, mod.GameRoot);
        return true;
    }

    public static bool IsAllowedTarget(GameId game, string target) => game switch
    {
        GameId.World => new[] { "dinput8.dll", "loader.dll", "loader-config.json", "nativePC/plugins/QuestLoader.dll", "nativePC/plugins/MonsterLoader.dll" }.Contains(target, StringComparer.OrdinalIgnoreCase),
        GameId.Rise => new[] { "dinput8.dll", "reframework/plugins/FirstNatives.dll" }.Contains(target, StringComparer.OrdinalIgnoreCase),
        _ => target.Equals("dinput8.dll", StringComparison.OrdinalIgnoreCase)
    };

    public static List<PackageFile> SelectFiles(PrerequisiteKind kind, IReadOnlyList<PackageFile> files)
    {
        PackageFile FindUnique(string name)
        {
            var matches = files.Where(f => Path.GetFileName(f.Source).Equals(name, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count != 1) throw new InvalidDataException($"前置压缩包需要唯一的 {name}");
            return matches[0];
        }
        var selected = new List<PackageFile>();
        if (kind == PrerequisiteKind.REFramework)
        {
            var dll = FindUnique("dinput8.dll");
            selected.Add(new PackageFile { Source = dll.Source, Target = "dinput8.dll", Size = dll.Size });
        }
        else if (kind == PrerequisiteKind.FirstNatives)
        {
            var dll = FindUnique("FirstNatives.dll");
            selected.Add(new PackageFile { Source = dll.Source, Target = "reframework/plugins/FirstNatives.dll", Size = dll.Size });
        }
        else if (kind == PrerequisiteKind.StrackersLoader)
        {
            foreach (var name in new[] { "dinput8.dll", "loader.dll" })
            {
                var file = FindUnique(name);
                selected.Add(new PackageFile { Source = file.Source, Target = name, Size = file.Size });
            }
            foreach (var optional in new[] { "loader-config.json", "QuestLoader.dll", "MonsterLoader.dll" })
            {
                var matches = files.Where(f => Path.GetFileName(f.Source).Equals(optional, StringComparison.OrdinalIgnoreCase)).ToList();
                if (matches.Count > 1) throw new InvalidDataException($"前置包中有重复的 {optional}");
                if (matches.Count == 1) selected.Add(new PackageFile { Source = matches[0].Source, Target = optional.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? "nativePC/plugins/" + optional : optional, Size = matches[0].Size });
            }
        }
        else throw new InvalidOperationException("Loose File Loader 需要游戏内设置，不能从压缩包安装");
        return selected;
    }

    public static async Task<string> DownloadOfficialREFrameworkAsync(string destination, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
        using var response = await client.GetAsync(REFrameworkUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        if (response.RequestMessage?.RequestUri?.Host is not ("github.com" or "release-assets.githubusercontent.com"))
            throw new InvalidDataException("下载地址不属于 GitHub 官方发布资源");
        await using var source = await response.Content.ReadAsStreamAsync(ct);
        await using var target = File.Create(destination);
        await source.CopyToAsync(target, ct);
        return destination;
    }

    private static bool IsLooseFileLoaderEnabled(string root)
    {
        var config = Path.Combine(root, "re2_fw_config.txt");
        if (!File.Exists(config)) return false;
        return File.ReadLines(config).Any(line =>
        {
            var parts = line.Split('=', 2);
            return parts.Length == 2 && parts[0].Trim().Equals("LooseFileLoader_Enabled", StringComparison.OrdinalIgnoreCase) && parts[1].Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
        });
    }
}
