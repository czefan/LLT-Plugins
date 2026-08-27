using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Controllers.Sensors;
using LenovoLegionToolkit.Lib.Station.Core;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.Resources;
using LenovoLegionToolkit.WPF.Windows.Utils;
using Application = System.Windows.Application;
using Control = System.Windows.Forms.Control;
using Label = System.Windows.Controls.Label;

namespace UiEnhancement.Features;

/// <summary>
/// 托盘状态气泡增强：免驱动显示 CPU/GPU 实时温度与风扇转速，精简冗余信息并自适应多显示器与屏幕边缘。
/// </summary>
public sealed class TrayStatusEnhancer : IUiFeature
{
    private readonly record struct SensorSnapshot(int CpuTemp, int GpuTemp, int CpuFan, int GpuFan);

    private static SensorsController? _sensors;
    private static SensorSnapshot? _cache;

    public void Initialize(IExtensionContext context) =>
        Application.Current?.Dispatcher.Invoke(() =>
            EventManager.RegisterClassHandler(typeof(StatusWindow), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnLoaded)));

    private static async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not StatusWindow win) return;

        try
        {
            win.SizeChanged -= OnWindowSizeChanged;
            win.SizeChanged += OnWindowSizeChanged;

            // 隐藏冗余控件（标题与系统风扇）
            if (win.FindName("_systemFanGrid") is FrameworkElement sysFan) sysFan.Visibility = Visibility.Collapsed;
            if (win.FindName("_title") is FrameworkElement title) title.Visibility = Visibility.Collapsed;

            // 折叠不常用的放电记录（第 3、4 行）
            if (win.FindName("_batteryMinDischargeValueLabel") is FrameworkElement { Parent: Grid batGrid })
            {
                foreach (UIElement child in batGrid.Children)
                    if (Grid.GetRow(child) is 3 or 4) child.Visibility = Visibility.Collapsed;

                for (var r = 3; r < batGrid.RowDefinitions.Count; r++)
                    batGrid.RowDefinitions[r].Height = new(0);
            }

            // 优先使用内存快照秒开呈现
            if (_cache is { } cached) RenderSensors(win, cached);
            else if (win.FindName("_cpuGrid") is Grid cpuGrid) cpuGrid.Visibility = Visibility.Visible;

            AdjustPosition(win);

            // 异步刷新最新传感器数据
            _sensors ??= IoCContainer.Resolve<SensorsController>();
            if (_sensors is not null && await _sensors.IsSupportedAsync().ConfigureAwait(true))
            {
                var data = await _sensors.GetDataAsync().ConfigureAwait(true);
                _cache = new(data.CPU.Temperature, data.GPU.Temperature, data.CPU.FanSpeed, data.GPU.FanSpeed);
                RenderSensors(win, _cache.Value);
                AdjustPosition(win);
            }
        }
        catch (Exception ex)
        {
            Log.Instance.Trace($"[TrayStatusEnhancer] OnLoaded error: {ex}");
        }
    }

    private static void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is StatusWindow win) AdjustPosition(win);
    }

    private static void RenderSensors(StatusWindow win, SensorSnapshot s)
    {
        void Update(string prefix, int temp, int fan)
        {
            if (win.FindName($"_{prefix}Grid") is Grid g) g.Visibility = Visibility.Visible;
            SetMetric(win, $"_{prefix}FreqAndTempDesc", $"_{prefix}FreqAndTempLabel", Resource.SensorsControl_Temperature_Title, temp > 0 ? $"{temp} °C" : "-");
            SetMetric(win, $"_{prefix}FanAndPowerDesc", $"_{prefix}FanAndPowerLabel", Resource.SensorsControl_Fan_Title, fan > 0 ? $"{fan} RPM" : "-");
        }

        Update("cpu", s.CpuTemp, s.CpuFan);
        if (s.GpuTemp > 0 || s.GpuFan > 0) Update("gpu", s.GpuTemp, s.GpuFan);
    }

    private static void SetMetric(StatusWindow win, string descName, string labelName, string title, string val)
    {
        if (win.FindName(descName) is Label desc) { desc.Visibility = Visibility.Visible; desc.Content = title; }
        if (win.FindName(labelName) is Label lbl) { lbl.Visibility = Visibility.Visible; lbl.Content = val; }
    }

    /// <summary>
    /// 支持多显示器与 Per-Monitor DPI 的屏幕边缘贴合调整
    /// </summary>
    private static void AdjustPosition(StatusWindow win)
    {
        try
        {
            win.UpdateLayout();
            var source = PresentationSource.FromVisual(win);
            if (source?.CompositionTarget is null) return;

            var matrix = source.CompositionTarget.TransformFromDevice;
            var area = Screen.FromPoint(Control.MousePosition).WorkingArea;

            var topLeft = matrix.Transform(new Point(area.Left, area.Top));
            var bottomRight = matrix.Transform(new Point(area.Right, area.Bottom));
            const double margin = 12.0;

            var minX = topLeft.X + margin;
            var minY = topLeft.Y + margin;
            var maxX = Math.Max(minX, bottomRight.X - win.ActualWidth - margin);
            var maxY = Math.Max(minY, bottomRight.Y - win.ActualHeight - margin);

            win.Left = Math.Clamp(win.Left, minX, maxX);
            win.Top = Math.Clamp(win.Top, minY, maxY);
        }
        catch (Exception ex)
        {
            Log.Instance.Trace($"[TrayStatusEnhancer] AdjustPosition error: {ex}");
        }
    }
}
