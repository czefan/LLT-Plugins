using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Threading;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Controllers.Sensors;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.Station.Core;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.Resources;
using LenovoLegionToolkit.WPF.Windows.Utils;
using Application = System.Windows.Application;
using Control = System.Windows.Forms.Control;
using Label = System.Windows.Controls.Label;
using Point = System.Windows.Point;

namespace UiEnhancement.Features;

/// <summary>
/// 托盘状态气泡增强：免驱动显示 CPU/GPU 实时温度与风扇转速，精简冗余信息并自适应多显示器与屏幕边缘。
/// </summary>
public sealed class TrayStatusEnhancer : IUiFeature
{
    private readonly record struct SensorSnapshot(int CpuTemp, int GpuTemp, int CpuFan, int GpuFan);

    private static SensorsController? _sensors;
    private static ApplicationSettings? _settings;
    private static SensorSnapshot? _cache;
    private static long _cacheTick;
    private const long CACHE_TTL_MS = 10_000;

    private static (int X, int Y) _anchor;
    private static bool _registered;
    private static bool _enabled;
    private static bool _isRefreshing;

    public void Initialize(IExtensionContext context)
    {
        _enabled = true;
        if (_registered) return;
        _registered = true;

        Application.Current?.Dispatcher.Invoke(() =>
            EventManager.RegisterClassHandler(typeof(StatusWindow), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnLoaded)));
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!_enabled || sender is not StatusWindow win) return;

        _anchor = (Control.MousePosition.X, Control.MousePosition.Y);

        ApplyLayout(win);
        if (TryGetFreshCache(out var cached))
            RenderSensors(win, cached);

        win.SizeChanged -= OnWindowSizeChanged;
        win.SizeChanged += OnWindowSizeChanged;

        // 排到宿主实例处理器之后执行贴边重算
        win.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => Reposition(win));

        // DispatcherTimer 保证每个宿主刷新周期后我们都能覆盖改回来；窗口 Closed 时停止
        var timer = new DispatcherTimer(DispatcherPriority.Background, win.Dispatcher)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        timer.Tick += (_, _) => _ = RefreshAsync(win);
        win.Closed += (_, _) => timer.Stop();
        timer.Start();

        _ = RefreshAsync(win);
    }

    private static async Task RefreshAsync(StatusWindow win)
    {
        if (_isRefreshing || !_enabled || !win.IsLoaded) return;
        _isRefreshing = true;

        try
        {
            _sensors ??= IoCContainer.Resolve<SensorsController>();
            if (_sensors is null || !await _sensors.IsSupportedAsync().ConfigureAwait(true))
            {
                _cache = null;
                return;
            }

            var data = await _sensors.GetDataAsync().ConfigureAwait(true);
            _cache = new(data.CPU.Temperature, data.GPU.Temperature, data.CPU.FanSpeed, data.GPU.FanSpeed);
            _cacheTick = Environment.TickCount64;

            if (!_enabled || !win.IsLoaded) return;

            ApplyLayout(win); // 覆盖宿主刚刚重置的 Visibility
            RenderSensors(win, _cache.Value);
            Reposition(win);
        }
        catch (Exception ex)
        {
            _cache = null;
            Log.Instance.Trace($"[TrayStatusEnhancer] RefreshAsync error: {ex}");
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private static void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_enabled && sender is StatusWindow win) Reposition(win);
    }

    private static bool TryGetFreshCache(out SensorSnapshot snapshot)
    {
        snapshot = default;
        if (_cache is not { } c) return false;
        if (Environment.TickCount64 - _cacheTick > CACHE_TTL_MS)
        {
            _cache = null;
            return false;
        }
        snapshot = c;
        return true;
    }

    private static void ApplyLayout(StatusWindow win)
    {
        // 隐藏冗余控件（标题与系统风扇）
        if (win.FindName("_systemFanGrid") is FrameworkElement sysFan) sysFan.Visibility = Visibility.Collapsed;
        if (win.FindName("_title") is FrameworkElement title) title.Visibility = Visibility.Collapsed;

        // 根据控件名精确定位并折叠电池放电历史记录行
        var minLabel = win.FindName("_batteryMinDischargeValueLabel") as FrameworkElement;
        var maxLabel = win.FindName("_batteryMaxDischargeValueLabel") as FrameworkElement;
        var batGrid = (minLabel?.Parent ?? maxLabel?.Parent) as Grid;

        if (batGrid != null)
        {
            var targetRows = new HashSet<int>();
            if (minLabel != null) targetRows.Add(Grid.GetRow(minLabel));
            if (maxLabel != null) targetRows.Add(Grid.GetRow(maxLabel));

            foreach (UIElement child in batGrid.Children)
            {
                if (targetRows.Contains(Grid.GetRow(child)))
                    child.Visibility = Visibility.Collapsed;
            }

            foreach (var row in targetRows)
            {
                if (row >= 0 && row < batGrid.RowDefinitions.Count)
                    batGrid.RowDefinitions[row].Height = new(0);
            }
        }
    }

    private static void RenderSensors(StatusWindow win, SensorSnapshot s)
    {
        void Update(string prefix, int temp, int fan)
        {
            if (win.FindName($"_{prefix}Grid") is Grid g) g.Visibility = Visibility.Visible;
            SetMetric(win, $"_{prefix}FreqAndTempDesc", $"_{prefix}FreqAndTempLabel", Resource.SensorsControl_Temperature_Title, FormatTemp(temp));
            SetMetric(win, $"_{prefix}FanAndPowerDesc", $"_{prefix}FanAndPowerLabel", Resource.SensorsControl_Fan_Title, FormatFan(fan));
        }

        Update("cpu", s.CpuTemp, s.CpuFan);
        if (s.GpuTemp > 0 || s.GpuFan > 0) Update("gpu", s.GpuTemp, s.GpuFan);
    }

    private static void SetMetric(StatusWindow win, string descName, string labelName, string title, string val)
    {
        if (win.FindName(descName) is Label desc) { desc.Visibility = Visibility.Visible; desc.Content = title; }
        if (win.FindName(labelName) is Label lbl) { lbl.Visibility = Visibility.Visible; lbl.Content = val; }
    }

    private static string FormatTemp(int c)
    {
        if (c <= 0) return "-";
        _settings ??= IoCContainer.Resolve<ApplicationSettings>();
        return _settings?.Store.TemperatureUnit == TemperatureUnit.F
            ? $"{Math.Round(c * 1.8 + 32):0}{Resource.Fahrenheit}"
            : $"{c:0}{Resource.Celsius}";
    }

    private static string FormatFan(int rpm) => rpm > 0 ? $"{rpm:0}{Resource.RPM}" : "-";

    private static void Reposition(StatusWindow win)
    {
        try
        {
            win.UpdateLayout();
            var source = PresentationSource.FromVisual(win);
            if (source?.CompositionTarget is null) return;

            var m = source.CompositionTarget.TransformFromDevice;
            var area = Screen.FromPoint(new System.Drawing.Point(_anchor.X, _anchor.Y)).WorkingArea;

            var mouse = m.Transform(new Point(_anchor.X, _anchor.Y));
            var lt = m.Transform(new Point(area.Left, area.Top));
            var rb = m.Transform(new Point(area.Right, area.Bottom));
            const double offset = 8.0;

            // 与宿主 MoveBottomRightEdgeOfWindowToMousePosition 保持一致的翻转逻辑
            win.Left = mouse.X + offset + win.ActualWidth > rb.X ? mouse.X - win.ActualWidth - offset : mouse.X + offset;
            win.Top = mouse.Y + offset + win.ActualHeight > rb.Y ? mouse.Y - win.ActualHeight - offset : mouse.Y + offset;

            win.Left = Math.Clamp(win.Left, lt.X, Math.Max(lt.X, rb.X - win.ActualWidth));
            win.Top = Math.Clamp(win.Top, lt.Y, Math.Max(lt.Y, rb.Y - win.ActualHeight));
        }
        catch (Exception ex)
        {
            Log.Instance.Trace($"[TrayStatusEnhancer] Reposition error: {ex}");
        }
    }

    public void Dispose()
    {
        _enabled = false;
        _cache = null;
    }
}
