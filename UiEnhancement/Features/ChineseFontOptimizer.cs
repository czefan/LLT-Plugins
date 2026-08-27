using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using LenovoLegionToolkit.Lib.Station.Core;
using LenovoLegionToolkit.WPF.Resources;
using LenovoLegionToolkit.WPF.Windows.Osd;

namespace UiEnhancement.Features;

/// <summary>
/// 模块：中文字体（微软雅黑）与 ClearType 文本渲染清晰度优化
/// </summary>
public sealed class ChineseFontOptimizer : IUiFeature
{
    private static readonly FontFamily YaHei = new("Microsoft YaHei UI, Microsoft YaHei, Segoe UI");
    private static bool _registered;
    private static bool _enabled;

    private static bool IsChinese =>
        Resource.Culture?.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ??
        CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);

    public void Initialize(IExtensionContext context)
    {
        _enabled = true;
        if (_registered) return;
        _registered = true;

        Application.Current?.Dispatcher.Invoke(() =>
        {
            EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
                new RoutedEventHandler((s, _) => { if (_enabled && s is Window w) Apply(w); }));

            EventManager.RegisterClassHandler(typeof(ContextMenu), FrameworkElement.LoadedEvent,
                new RoutedEventHandler((s, _) =>
                {
                    if (_enabled && s is ContextMenu cm && IsChinese)
                        SetFont(cm, YaHei);
                }));

            EventManager.RegisterClassHandler(typeof(ToolTip), FrameworkElement.LoadedEvent,
                new RoutedEventHandler((s, _) =>
                {
                    if (_enabled && s is ToolTip tt && IsChinese)
                        SetFont(tt, YaHei);
                }));

            if (Application.Current.MainWindow is { } main)
                Apply(main);
        });
    }

    public static void Apply(Window win)
    {
        if (win is OsdWindowBase) return;
        if (win.AllowsTransparency && win.WindowStyle == WindowStyle.None && win is not Wpf.Ui.Controls.UiWindow) return;

        var font = IsChinese ? YaHei : null;
        SetFont(win, font);

        if (win.Content is FrameworkElement content)
            SetFont(content, font);

        // 排到 Loaded 队列末尾，确保能读到 UiScaler 已写入的 transform
        win.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () => ApplyTextOptions(win));
    }

    public static void ApplyTextOptions(Window win)
    {
        var scale = (win.Content as FrameworkElement)?.LayoutTransform is ScaleTransform st ? st.ScaleX : 1.0;
        var crisp = Math.Abs(scale % 1.0) < 0.001; // 1.0 / 2.0 才走 Display

        win.UseLayoutRounding = crisp;
        win.SnapsToDevicePixels = crisp;
        TextOptions.SetTextFormattingMode(win, crisp ? TextFormattingMode.Display : TextFormattingMode.Ideal);
        TextOptions.SetTextRenderingMode(win, TextRenderingMode.ClearType);
        RenderOptions.SetBitmapScalingMode(win, BitmapScalingMode.HighQuality);
    }

    public static void SetFont(DependencyObject target, FontFamily? font)
    {
        if (font is not null)
            target.SetValue(TextElement.FontFamilyProperty, font);
        else
            target.ClearValue(TextElement.FontFamilyProperty);
    }

    public void Dispose()
    {
        _enabled = false;
        Application.Current?.Dispatcher.Invoke(() =>
        {
            foreach (Window w in Application.Current.Windows)
            {
                SetFont(w, null);
                if (w.Content is FrameworkElement c) SetFont(c, null);
            }
        });
    }
}
