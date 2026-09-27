using System.IO;
using System.Text.RegularExpressions;
using HunterModManager.Core;
using Microsoft.Win32;

namespace HunterModManager.Wpf.Helpers;

/// <summary>
/// Locates Steam game installation directories via registry and libraryfolders.vdf.
/// Ported from the WinForms version.
/// </summary>
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
