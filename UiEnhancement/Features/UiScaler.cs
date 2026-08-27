using System;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;
using LenovoLegionToolkit.Lib.Station.Core;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.Controls;
using LenovoLegionToolkit.WPF.Controls.Custom;
using LenovoLegionToolkit.WPF.Controls.Settings;
using LenovoLegionToolkit.WPF.Resources;
using LenovoLegionToolkit.WPF.Windows.Osd;
using Wpf.Ui.Common;
using Application = System.Windows.Application;
using ComboBox = System.Windows.Controls.ComboBox;
using ContextMenu = System.Windows.Controls.ContextMenu;
using Point = System.Windows.Point;
using ToolTip = System.Windows.Controls.ToolTip;

namespace UiEnhancement.Features;

/// <summary>
/// 模块：界面矢量缩放与设置面板注入
/// </summary>
public sealed class UiScaler : IUiFeature
{
    private const string PluginId = "UiEnhancement.Plugin";
    private static readonly (string Text, double Val)[] Scales =
    [
        ("100%", 1.00), ("110%", 1.10), ("125%", 1.25),
        ("135%", 1.35), ("150%", 1.50), ("175%", 1.75), ("200%", 2.00)
    ];

    private static readonly ConditionalWeakTable<Window, WindowBounds> BaseSizes = [];
    private static IExtensionContext? _context;
    private static double _scale = 1.0;
    private static bool _registered;
    private static bool _enabled;

    public static double Scale => _scale;

    public static ScaleTransform CurrentTransform
    {
        get
        {
            var transform = new ScaleTransform(_scale, _scale);
            transform.Freeze();
            return transform;
        }
    }

    private static bool IsChinese =>
        Resource.Culture?.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ??
        CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);

    public void Initialize(IExtensionContext context)
    {
        _context = context;
        _enabled = true;
        _scale = LoadScale();

        if (_registered) return;
        _registered = true;

        Application.Current?.Dispatcher.Invoke(() =>
        {
            EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
                new RoutedEventHandler((s, _) => { if (_enabled && s is Window w) Apply(w); }));

            EventManager.RegisterClassHandler(typeof(SettingsAppearanceControl), FrameworkElement.LoadedEvent,
                new RoutedEventHandler((s, _) => { if (_enabled && s is SettingsAppearanceControl c) InjectSettings(c); }));

            RegisterPopupHandlers();

            if (Application.Current.MainWindow is { } main)
                Apply(main);
        });
    }

    private static void RegisterPopupHandlers()
    {
        EventManager.RegisterClassHandler(typeof(ContextMenu), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((s, _) => { if (_enabled && s is ContextMenu c) c.LayoutTransform = CurrentTransform; }));

        EventManager.RegisterClassHandler(typeof(ToolTip), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((s, _) => { if (_enabled && s is ToolTip t) t.LayoutTransform = CurrentTransform; }));

        EventManager.RegisterClassHandler(typeof(Popup), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((s, _) =>
            {
                if (s is Popup p)
                {
                    p.Opened -= OnPopupOpened;
                    p.Opened += OnPopupOpened;
                    if (_enabled && p.Child is FrameworkElement child)
                        child.LayoutTransform = CurrentTransform;
                }
            }));
    }

    private static void OnPopupOpened(object? sender, EventArgs e)
    {
        if (_enabled && sender is Popup { Child: FrameworkElement child })
            child.LayoutTransform = CurrentTransform;
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
        _scale = Math.Clamp(scale, 1.0, 2.0);
        SaveScale(_scale);

        Application.Current?.Dispatcher.Invoke(() =>
        {
            foreach (Window w in Application.Current.Windows)
            {
                Apply(w);
            }
        });
    }

    public static void Apply(Window win)
    {
        if (win is OsdWindowBase) return;
        if (win.AllowsTransparency && win.WindowStyle == WindowStyle.None && win is not Wpf.Ui.Controls.UiWindow) return;

        if (win.Content is FrameworkElement content)
            content.LayoutTransform = CurrentTransform;

        // 根据缩放倍率自动设置文本渲染模式，与 ChineseFontOptimizer 保持解耦
        var crisp = Math.Abs(_scale % 1.0) < 0.001;
        win.UseLayoutRounding = crisp;
        win.SnapsToDevicePixels = crisp;
        TextOptions.SetTextFormattingMode(win, crisp ? TextFormattingMode.Display : TextFormattingMode.Ideal);

        var bs = BaseSizes.GetValue(win, w =>
        {
            var width = !double.IsNaN(w.Width) && w.Width > 0 ? w.Width : (w.ActualWidth > 0 ? w.ActualWidth : double.NaN);
            var height = !double.IsNaN(w.Height) && w.Height > 0 ? w.Height : (w.ActualHeight > 0 ? w.ActualHeight : double.NaN);
            return new WindowBounds(width, height, w.MinWidth, w.MinHeight, w.MaxWidth, w.MaxHeight);
        });

        var work = GetWorkArea(win);
        double maxW = work.Width * 0.96, maxH = work.Height * 0.96;

        static double ScaleVal(double v, double max) => !double.IsNaN(v) && !double.IsInfinity(v) && v > 0 ? Math.Min(v * _scale, max) : double.NaN;

        if (ScaleVal(bs.MaxW, work.Width) is var mxw && !double.IsNaN(mxw)) win.MaxWidth = mxw;
        if (ScaleVal(bs.MaxH, work.Height) is var mxh && !double.IsNaN(mxh)) win.MaxHeight = mxh;

        if (win.WindowState == WindowState.Normal && win.SizeToContent == SizeToContent.Manual)
        {
            if (ScaleVal(bs.W, maxW) is var w && !double.IsNaN(w)) win.Width = w;
            if (ScaleVal(bs.H, maxH) is var h && !double.IsNaN(h)) win.Height = h;

            if (win.WindowStartupLocation == WindowStartupLocation.CenterOwner && win.Owner is { } owner)
            {
                win.Left = owner.Left + (owner.ActualWidth - win.Width) / 2;
                win.Top = owner.Top + (owner.ActualHeight - win.Height) / 2;
            }

            if (!double.IsNaN(win.Left) && !double.IsNaN(win.Width))
            {
                if (win.Left + win.Width > work.Right) win.Left = Math.Max(work.Left, work.Right - win.Width);
                if (win.Left < work.Left) win.Left = work.Left;
            }

            if (!double.IsNaN(win.Top) && !double.IsNaN(win.Height))
            {
                if (win.Top + win.Height > work.Bottom) win.Top = Math.Max(work.Top, work.Bottom - win.Height);
                if (win.Top < work.Top) win.Top = work.Top;
            }
        }

        if (ScaleVal(bs.MinW, maxW) is var minW && !double.IsNaN(minW)) win.MinWidth = minW;
        if (ScaleVal(bs.MinH, maxH) is var minH && !double.IsNaN(minH)) win.MinHeight = minH;
    }

    private static Rect GetWorkArea(Window win)
    {
        try
        {
            var targetWin = (win.WindowStartupLocation == WindowStartupLocation.CenterOwner && win.Owner is { } owner) ? owner : win;
            var source = PresentationSource.FromVisual(targetWin) ?? PresentationSource.FromVisual(win);
            if (source?.CompositionTarget is null) return SystemParameters.WorkArea;

            var handle = new WindowInteropHelper(targetWin).Handle;
            if (handle == IntPtr.Zero) handle = new WindowInteropHelper(win).Handle;

            var area = handle == IntPtr.Zero
                ? Screen.PrimaryScreen?.WorkingArea ?? new System.Drawing.Rectangle(0, 0, (int)SystemParameters.WorkArea.Width, (int)SystemParameters.WorkArea.Height)
                : Screen.FromHandle(handle).WorkingArea;

            var m = source.CompositionTarget.TransformFromDevice;
            var lt = m.Transform(new Point(area.Left, area.Top));
            var rb = m.Transform(new Point(area.Right, area.Bottom));
            return new Rect(lt, rb);
        }
        catch
        {
            return SystemParameters.WorkArea;
        }
    }

    private static void InjectSettings(SettingsAppearanceControl ctrl)
    {
        if (ctrl.Content is not ScrollViewer { Content: StackPanel panel }) return;
        for (var i = 0; i < panel.Children.Count; i++)
            if (panel.Children[i] is FrameworkElement { Name: "ScaleCardControl" }) return;

        var combo = new ComboBox { MinWidth = 160, Margin = new Thickness(0, 0, 0, 8) };
        var matchedIndex = -1;
        for (var i = 0; i < Scales.Length; i++)
        {
            combo.Items.Add(Scales[i].Text);
            if (Math.Abs(Scales[i].Val - _scale) < 0.03) matchedIndex = i;
        }

        if (matchedIndex >= 0)
        {
            combo.SelectedIndex = matchedIndex;
        }
        else
        {
            var customText = $"{_scale * 100:0}%";
            combo.Items.Add(customText);
            combo.SelectedIndex = combo.Items.Count - 1;
        }

        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedIndex >= 0 && combo.SelectedIndex < Scales.Length)
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

    private static string GetConfigDirectory()
    {
        try
        {
            if (_context is not null)
                return _context.GetPluginStoragePath(PluginId);
        }
        catch { }

        var defaultPath = Path.Combine(Folders.AppData, "Plugins", "Configs", PluginId);
        Directory.CreateDirectory(defaultPath);
        return defaultPath;
    }

    private static string ConfigPath => Path.Combine(GetConfigDirectory(), "config.json");

    private static double LoadScale()
    {
        try
        {
            var file = ConfigPath;
            if (File.Exists(file))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file));
                if (doc.RootElement.TryGetProperty("Scale", out var prop) && prop.TryGetDouble(out var v) && v is >= 1.0 and <= 2.0)
                    return v;
            }
        }
        catch (Exception ex)
        {
            _context?.Logger?.Error("[UiEnhancement] Failed to load scale setting", ex);
        }
        return 1.0;
    }

    private static void SaveScale(double scale)
    {
        try
        {
            var dir = GetConfigDirectory();
            Directory.CreateDirectory(dir);
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
        _enabled = false;
        Application.Current?.Dispatcher.Invoke(() =>
        {
            foreach (Window w in Application.Current.Windows)
            {
                if (w.Content is FrameworkElement c)
                    c.LayoutTransform = Transform.Identity;

                if (BaseSizes.TryGetValue(w, out var bs))
                {
                    if (!double.IsNaN(bs.W)) w.Width = bs.W;
                    if (!double.IsNaN(bs.H)) w.Height = bs.H;
                    if (!double.IsNaN(bs.MinW)) w.MinWidth = bs.MinW;
                    if (!double.IsNaN(bs.MinH)) w.MinHeight = bs.MinH;
                    if (!double.IsNaN(bs.MaxW)) w.MaxWidth = bs.MaxW;
                    if (!double.IsNaN(bs.MaxH)) w.MaxHeight = bs.MaxH;
                }
            }
        });
    }

    private sealed record WindowBounds(double W, double H, double MinW, double MinH, double MaxW, double MaxH);
}
