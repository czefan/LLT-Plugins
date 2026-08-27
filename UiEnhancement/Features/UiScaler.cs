using System;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LenovoLegionToolkit.Lib.Station.Core;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.Controls;
using LenovoLegionToolkit.WPF.Controls.Custom;
using LenovoLegionToolkit.WPF.Controls.Settings;
using LenovoLegionToolkit.WPF.Resources;
using Wpf.Ui.Common;

namespace UiEnhancement.Features;

/// <summary>
/// 模块：界面矢量缩放与设置面板注入
/// </summary>
public sealed class UiScaler : IUiFeature
{
    private static readonly string ConfigPath = Path.Combine(Folders.AppData, "ui_enhancement.json");
    private static readonly (string Text, double Val)[] Scales =
    [
        ("100%", 1.00), ("110%", 1.10), ("125%", 1.25),
        ("135%", 1.35), ("150%", 1.50), ("175%", 1.75), ("200%", 2.00)
    ];

    private static readonly ConditionalWeakTable<Window, WindowBounds> BaseSizes = [];
    private static IExtensionContext? _context;
    private static double _scale = 1.25;

    public static double Scale => _scale;

    private static bool IsChinese =>
        Resource.Culture?.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ??
        CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);

    public void Initialize(IExtensionContext context)
    {
        _context = context;
        _scale = LoadScale();

        Application.Current?.Dispatcher.Invoke(() =>
        {
            EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
                new RoutedEventHandler((s, _) => { if (s is Window w) Apply(w); }));
            EventManager.RegisterClassHandler(typeof(SettingsAppearanceControl), FrameworkElement.LoadedEvent,
                new RoutedEventHandler((s, _) => { if (s is SettingsAppearanceControl c) InjectSettings(c); }));

            if (Application.Current.MainWindow is { } main)
                Apply(main);
        });
    }

    public object? GetData(string key) => key == "Scale" ? _scale : null;

    public bool SetData(string key, object? value)
    {
        if (key == "Scale" && value is double d)
        {
            SetScale(d);
            return true;
        }
        return false;
    }

    public static void SetScale(double scale)
    {
        _scale = Math.Clamp(scale, 0.8, 3.0);
        SaveScale(_scale);

        Application.Current?.Dispatcher.Invoke(() =>
        {
            foreach (Window w in Application.Current.Windows)
                Apply(w);
        });
    }

    public static void Apply(Window win)
    {
        if (win.GetType().FullName?.Contains(".Osd.", StringComparison.OrdinalIgnoreCase) == true) return;
        if (win.AllowsTransparency && win.WindowStyle == WindowStyle.None && win is not Wpf.Ui.Controls.UiWindow) return;

        if (win.Content is FrameworkElement content)
        {
            var transform = new ScaleTransform(_scale, _scale);
            transform.Freeze();
            content.LayoutTransform = transform;
        }

        var bs = BaseSizes.GetValue(win, w =>
        {
            var width = !double.IsNaN(w.Width) && w.Width > 0 ? w.Width : (w.ActualWidth > 0 ? w.ActualWidth : double.NaN);
            var height = !double.IsNaN(w.Height) && w.Height > 0 ? w.Height : (w.ActualHeight > 0 ? w.ActualHeight : double.NaN);
            return new WindowBounds(width, height, w.MinWidth, w.MinHeight, w.MaxWidth, w.MaxHeight);
        });

        var work = SystemParameters.WorkArea;
        double maxW = work.Width * 0.96, maxH = work.Height * 0.96;

        static double ScaleVal(double v, double max) => !double.IsNaN(v) && !double.IsInfinity(v) && v > 0 ? Math.Min(v * _scale, max) : double.NaN;

        if (ScaleVal(bs.MaxW, work.Width) is var mxw && !double.IsNaN(mxw)) win.MaxWidth = mxw;
        if (ScaleVal(bs.MaxH, work.Height) is var mxh && !double.IsNaN(mxh)) win.MaxHeight = mxh;

        if (win.WindowState == WindowState.Normal && win.SizeToContent == SizeToContent.Manual)
        {
            if (ScaleVal(bs.W, maxW) is var w && !double.IsNaN(w)) win.Width = w;
            if (ScaleVal(bs.H, maxH) is var h && !double.IsNaN(h)) win.Height = h;
            if (win.Left + win.Width > work.Right) win.Left = Math.Max(work.Left, work.Right - win.Width);
            if (win.Top + win.Height > work.Bottom) win.Top = Math.Max(work.Top, work.Bottom - win.Height);
        }

        if (ScaleVal(bs.MinW, maxW) is var minW && !double.IsNaN(minW)) win.MinWidth = minW;
        if (ScaleVal(bs.MinH, maxH) is var minH && !double.IsNaN(minH)) win.MinHeight = minH;
    }

    private static void InjectSettings(SettingsAppearanceControl ctrl)
    {
        if (ctrl.Content is not ScrollViewer { Content: StackPanel panel }) return;
        for (var i = 0; i < panel.Children.Count; i++)
            if (panel.Children[i] is FrameworkElement { Name: "ScaleCardControl" }) return;

        var combo = new ComboBox { MinWidth = 160, Margin = new Thickness(0, 0, 0, 8) };
        for (var i = 0; i < Scales.Length; i++)
        {
            combo.Items.Add(Scales[i].Text);
            if (Math.Abs(Scales[i].Val - _scale) < 0.03) combo.SelectedIndex = i;
        }

        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedIndex >= 0)
                SetScale(Scales[combo.SelectedIndex].Val);
        };

        var isZh = IsChinese;
        panel.Children.Insert(Math.Min(2, panel.Children.Count), new CardControl
        {
            Name = "ScaleCardControl",
            Margin = new Thickness(0, 0, 0, 8),
            Icon = SymbolRegular.ZoomIn24,
            Header = new CardHeaderControl
            {
                Title = isZh ? "界面缩放" : "UI Scale",
                Subtitle = isZh ? "调整界面的矢量缩放倍率" : "Adjust interface scaling factor"
            },
            Content = combo
        });
    }

    private static double LoadScale()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(ConfigPath));
                if (doc.RootElement.TryGetProperty("Scale", out var prop) && prop.TryGetDouble(out var v) && v is >= 0.8 and <= 3.0)
                    return v;
            }
        }
        catch (Exception ex)
        {
            _context?.Logger?.Error("[UiEnhancement] Failed to load scale setting", ex);
        }
        return 1.25;
    }

    private static void SaveScale(double scale)
    {
        try
        {
            Directory.CreateDirectory(Folders.AppData);
            var json = JsonSerializer.Serialize(new { Scale = scale }, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
        catch (Exception ex)
        {
            _context?.Logger?.Error("[UiEnhancement] Failed to save scale setting", ex);
        }
    }

    public void Dispose()
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            foreach (Window w in Application.Current.Windows)
                if (w.Content is FrameworkElement c)
                    c.LayoutTransform = Transform.Identity;
        });
    }

    private sealed record WindowBounds(double W, double H, double MinW, double MinH, double MaxW, double MaxH);
}
