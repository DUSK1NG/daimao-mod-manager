using HunterModManager.Core;
using HunterModManager.Wpf.Helpers;

namespace HunterModManager.Wpf.ViewModels;

/// <summary>
/// ViewModel wrapper around a single ModRecord for display in the Mod list.
/// </summary>
public sealed class ModItemViewModel : ViewModelBase
{
    private readonly ModRecord record;
    private bool enabled;

    public ModItemViewModel(ModRecord record)
    {
        this.record = record;
        enabled = record.Enabled;
    }

    public string Id => record.Id;
    public string Name => record.Name;
    public string Variant => record.Variant;
    public int FileCount => record.Files.Count;
    public bool IsPrerequisite => record.IsPrerequisite;
    public string Sha256 => record.Sha256;
    public IReadOnlyList<string> Requirements => record.Requirements;
    public IReadOnlyList<PackageFile> Files => record.Files;

    public bool Enabled
    {
        get => enabled;
        set
        {
            if (SetField(ref enabled, value))
            {
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    public string StatusText => Enabled ? "已启用" : "已停用";

    public string FilesSummary => $"{FileCount} 个文件";

    public string DetailLines
    {
        get
        {
            var lines = new List<string>
            {
                Name,
                "版本：" + Variant,
                "状态：" + StatusText,
                "包哈希：" + Sha256
            };
            lines.AddRange(Requirements.Select(x => "前置：" + x));
            lines.AddRange(Files.Select(x => x.Source + "  →  " + x.Target));
            return string.Join(Environment.NewLine, lines);
        }
    }
}
