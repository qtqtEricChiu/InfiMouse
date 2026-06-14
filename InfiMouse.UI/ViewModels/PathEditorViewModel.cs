using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InfiMouse.Model;
using System.Collections.ObjectModel;

namespace InfiMouse.UI;

/// <summary>
/// 路径编辑器 ViewModel：管理画布上的路径点集合与选中状态。
/// </summary>
public partial class PathEditorViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<PathPoint> _pathPoints = new();

    [ObservableProperty]
    private int _selectedIndex = -1;

    [ObservableProperty]
    private double _canvasScale = 1.0;

    [ObservableProperty]
    private double _canvasOffsetX;

    [ObservableProperty]
    private double _canvasOffsetY;

    /// <summary>添加路径点（自动触发 PropertyChanged）。</summary>
    [RelayCommand]
    private void AddPoint(PathPoint point)
    {
        PathPoints.Add(point);
    }

    /// <summary>移除指定索引的路径点。</summary>
    [RelayCommand]
    private void RemovePointAt(int index)
    {
        if (index >= 0 && index < PathPoints.Count)
        {
            PathPoints.RemoveAt(index);
            if (SelectedIndex == index)
                SelectedIndex = -1;
            else if (SelectedIndex > index)
                SelectedIndex--;
        }
    }

    /// <summary>移除选中的路径点。</summary>
    [RelayCommand]
    private void RemoveSelected()
    {
        if (SelectedIndex >= 0 && SelectedIndex < PathPoints.Count)
        {
            PathPoints.RemoveAt(SelectedIndex);
            SelectedIndex = -1;
        }
    }

    /// <summary>清空所有路径点。</summary>
    [RelayCommand]
    private void ClearAll()
    {
        PathPoints.Clear();
        SelectedIndex = -1;
    }

    /// <summary>选中指定索引的路径点。</summary>
    public void SelectAt(int index)
    {
        SelectedIndex = (index >= 0 && index < PathPoints.Count) ? index : -1;
    }

    /// <summary>获取第一个路径点（起点）。</summary>
    public PathPoint? StartPoint => PathPoints.Count > 0 ? PathPoints[0] : null;

    /// <summary>获取最后一个路径点（终点）。</summary>
    public PathPoint? EndPoint => PathPoints.Count >= 2 ? PathPoints[^1] : null;

    /// <summary>将画布坐标转换为逻辑坐标。</summary>
    public (double x, double y) CanvasToLogical(double canvasX, double canvasY)
    {
        return ((canvasX - CanvasOffsetX) / CanvasScale, (canvasY - CanvasOffsetY) / CanvasScale);
    }

    /// <summary>将逻辑坐标转换为画布坐标。</summary>
    public (double x, double y) LogicalToCanvas(double logicalX, double logicalY)
    {
        return (logicalX * CanvasScale + CanvasOffsetX, logicalY * CanvasScale + CanvasOffsetY);
    }
}
