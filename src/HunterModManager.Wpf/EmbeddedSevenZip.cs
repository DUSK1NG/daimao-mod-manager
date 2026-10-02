using System.IO;
using System.Reflection;
using System.Security.Cryptography;

namespace HunterModManager.Wpf;

internal static class EmbeddedSevenZip
{
    private const string ResourcePrefix = "HunterModManager.Tools.";
    private static readonly string[] Files = ["7z.exe", "7z.dll", "License.txt", "THIRD-PARTY-NOTICES.md"];

    internal static string? ExtractTo(string dataRoot)
    {
        var assembly = Assembly.GetExecutingAssembly();
        if (assembly.GetManifestResourceInfo(ResourcePrefix + "7z.exe") is null) return null;

        using var executable = OpenResource(assembly, "7z.exe");
        var version = Convert.ToHexString(SHA256.HashData(executable))[..16];
        var toolsRoot = Path.Combine(dataRoot, "tools");
        var directory = Path.Combine(toolsRoot, version);
        CreateSafeDirectory(dataRoot);
        CreateSafeDirectory(toolsRoot);
        CreateSafeDirectory(directory);

        foreach (var name in Files)
        {
            using var resource = OpenResource(assembly, name);
            var expectedHash = SHA256.HashData(resource);
            var destination = Path.Combine(directory, name);
            if (File.Exists(destination))
            {
                if ((File.GetAttributes(destination) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException($"工具缓存包含链接文件：{destination}");
                using var cached = File.OpenRead(destination);
                if (SHA256.HashData(cached).SequenceEqual(expectedHash)) continue;
            }
            var temporary = Path.Combine(directory, $".{name}.{Guid.NewGuid():N}.tmp");
            try
            {
                resource.Position = 0;
                using (var output = File.Create(temporary)) resource.CopyTo(output);
                File.Move(temporary, destination, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
        return Path.Combine(directory, "7z.exe");
    }

    private static Stream OpenResource(Assembly assembly, string name)
        => assembly.GetManifestResourceStream(ResourcePrefix + name)
            ?? throw new InvalidOperationException($"发布包缺少内置 7-Zip 组件：{name}");

    private static void CreateSafeDirectory(string path)
    {
        if (Directory.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException($"工具缓存目录为链接：{path}");
        Directory.CreateDirectory(path);
    }
}
