using CommunityToolkit.Mvvm.ComponentModel;
using MathFuzzy.Core.Matrix;
using MathFuzzy.Desktop.Services;

namespace MathFuzzy.Desktop.ViewModels.Data;

public sealed partial class DataPanelViewModel : ObservableObject
{
    private readonly IWorkspaceService _workspace;

    [ObservableProperty] private string _tLevelsLabel = "T levels";
    [ObservableProperty] private int    _rows;
    [ObservableProperty] private int    _cols;
    [ObservableProperty] private bool   _hasToleranceMatrix;
    [ObservableProperty] private int    _tLevels;
    [ObservableProperty] private bool   _hasLabels;
    [ObservableProperty] private int    _classCount;
    [ObservableProperty] private string _classNames = "";

    public DataPanelViewModel(IWorkspaceService workspace)
    {
        _workspace = workspace;
        _workspace.DataChanged += OnDataChanged;
    }

    private void OnDataChanged(object? sender, EventArgs e)
    {
        if (_workspace.RawData is { } data)
        {
            Rows = data.Rows;
            Cols = data.Cols;
        }
        HasToleranceMatrix = _workspace.ToleranceMatrix is not null;

        if (_workspace.ToleranceMatrix is { } tm)
        {
            var distinct = new HashSet<double>();
            for (int i = 0; i < tm.Size; i++)
                for (int j = i + 1; j < tm.Size; j++)
                    distinct.Add(System.Math.Round(tm[i, j], 6));
            TLevels = distinct.Count;
            TLevelsLabel = _workspace.ActiveWorkset?.ClosureApplied == true ? "T̃ levels" : "T levels";
        }
        else
        {
            TLevels = 0;
        }

        if (_workspace.Labels is { } lbl)
        {
            HasLabels  = true;
            ClassCount = lbl.ClassCount;
            ClassNames = string.Join(", ", lbl.ClassNames);
        }
        else
        {
            HasLabels  = false;
            ClassCount = 0;
            ClassNames = "";
        }
    }
}
