using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using LenovoLegionToolkit.Lib.Station.Core;
using LenovoLegionToolkit.WPF.Resources;

namespace UiEnhancement.Features;

/// <summary>
/// 模块：中文字体（微软雅黑）与 ClearType 文本渲染清晰度优化
/// </summary>
public sealed class ChineseFontOptimizer : IUiFeature
{
    private static readonly FontFamily YaHei = new("Microsoft YaHei UI, Microsoft YaHei, Segoe UI");

    private static bool IsChinese =>
        Resource.Culture?.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ??
        CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);

    public void Initialize(IExtensionContext context)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
                new RoutedEventHandler((s, _) => { if (s is Window w) Apply(w); }));

            if (Application.Current.MainWindow is { } main)
                Apply(main);
        });
    }

    public static void Apply(Window win)
    {
        if (win.GetType().FullName?.Contains(".Osd.", StringComparison.OrdinalIgnoreCase) == true) return;
        if (win.AllowsTransparency && win.WindowStyle == WindowStyle.None && win is not Wpf.Ui.Controls.UiWindow) return;

        var font = IsChinese ? YaHei : null;
        SetFont(win, font);

        win.UseLayoutRounding = true;
        win.SnapsToDevicePixels = true;
        TextOptions.SetTextFormattingMode(win, TextFormattingMode.Display);
        TextOptions.SetTextRenderingMode(win, TextRenderingMode.ClearType);
        RenderOptions.SetBitmapScalingMode(win, BitmapScalingMode.HighQuality);

        if (win.Content is FrameworkElement content)
            SetFont(content, font);
    }

    public static void SetFont(DependencyObject target, FontFamily? font)
    {
        if (font is not null)
        {
            if (target is Control c) c.FontFamily = font;
            target.SetValue(TextElement.FontFamilyProperty, font);
        }
        else
        {
            if (target is Control c) c.ClearValue(Control.FontFamilyProperty);
            target.ClearValue(TextElement.FontFamilyProperty);
        }
    }

    public void Dispose()
    {
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
