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

        var resources = Files.ToDictionary(name => name, name => ReadResource(assembly, name));
        var version = Convert.ToHexString(SHA256.HashData(resources["7z.exe"])).Substring(0, 16);
        var toolsRoot = Path.Combine(dataRoot, "tools");
        var directory = Path.Combine(toolsRoot, version);
        CreateSafeDirectory(dataRoot);
        CreateSafeDirectory(toolsRoot);
        CreateSafeDirectory(directory);

        foreach (var (name, bytes) in resources)
        {
            var destination = Path.Combine(directory, name);
            if (File.Exists(destination))
            {
                if ((File.GetAttributes(destination) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException($"工具缓存包含链接文件：{destination}");
                if (SHA256.HashData(File.ReadAllBytes(destination)).SequenceEqual(SHA256.HashData(bytes))) continue;
            }
            var temporary = Path.Combine(directory, $".{name}.{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllBytes(temporary, bytes);
                File.Move(temporary, destination, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
        return Path.Combine(directory, "7z.exe");
    }

    private static byte[] ReadResource(Assembly assembly, string name)
    {
        using var stream = assembly.GetManifestResourceStream(ResourcePrefix + name)
            ?? throw new InvalidOperationException($"发布包缺少内置 7-Zip 组件：{name}");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static void CreateSafeDirectory(string path)
    {
        if (Directory.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException($"工具缓存目录为链接：{path}");
        Directory.CreateDirectory(path);
    }
}
