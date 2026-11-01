using CommunityToolkit.Mvvm.ComponentModel;
using MathFuzzy.Desktop.Services;

namespace MathFuzzy.Desktop.ViewModels.Workspace;

public enum WorksetStatus { Idle, Running, Done, Error }

public sealed partial class WorksetViewModel : ObservableObject
{
    [ObservableProperty] string _name = "";
    [ObservableProperty] string _meta = "";
    [ObservableProperty] WorksetStatus _status = WorksetStatus.Idle;
    [ObservableProperty] bool _isSelected;
    [ObservableProperty] bool _isMatrixInput;
    [ObservableProperty] bool _isChecked;

    /// <summary>Backing service workset.</summary>
    public Workset? Model { get; init; }

    public string StatusLabel => Status switch
    {
        WorksetStatus.Running => "running…",
        WorksetStatus.Done    => "done",
        WorksetStatus.Error   => "error",
        _                     => "idle"
    };

    partial void OnStatusChanged(WorksetStatus value) =>
        OnPropertyChanged(nameof(StatusLabel));

    public override string ToString() => $"{Name} ({Meta}), {StatusLabel}";

    public void RefreshMeta()
    {
        if (Model is { } m) Meta = m.Meta;
    }
}
