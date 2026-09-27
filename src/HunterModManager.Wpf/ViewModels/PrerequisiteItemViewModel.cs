using HunterModManager.Core;
using HunterModManager.Wpf.Helpers;

namespace HunterModManager.Wpf.ViewModels;

/// <summary>
/// ViewModel for a prerequisite component card.
/// </summary>
public sealed class PrerequisiteItemViewModel : ViewModelBase
{
    private bool isReady;
    private string status = "";

    public PrerequisiteKind Kind { get; init; }
    public string DisplayName => Prerequisites.Display(Kind);

    public bool IsReady
    {
        get => isReady;
        set => SetField(ref isReady, value);
    }

    public string Status
    {
        get => status;
        set => SetField(ref status, value);
    }

    /// <summary>Whether this component can be installed via the UI (excludes WildsLooseFileLoader).</summary>
    public bool CanInstall => !IsReady && Kind != PrerequisiteKind.WildsLooseFileLoader;

    public string ButtonText => Kind == PrerequisiteKind.REFramework
        ? "从官方 GitHub 下载并配置"
        : "导入已下载的压缩包…";

    public void Refresh(string gameRoot)
    {
        IsReady = Prerequisites.IsReady(Kind, gameRoot);
        Status = Prerequisites.Status(Kind, gameRoot);
        OnPropertyChanged(nameof(CanInstall));
    }
}
