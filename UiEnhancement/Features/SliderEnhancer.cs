using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using LenovoLegionToolkit.Lib.Station.Core;
using LenovoLegionToolkit.WPF.Controls.Dashboard.GodMode;

namespace UiEnhancement.Features;

/// <summary>
/// 模块：自定义模式滑动条外观重构
/// - 左侧显示最小值 (Min Value)
/// - 右侧显示最大值 (Max Value)
/// - 重构 Thumb 控件模板，利用 WPF 原生数据绑定与绝对定位 Canvas 将当前数值同轴居中悬浮于圆点正上方
/// </summary>
public sealed class SliderEnhancer : IUiFeature
{
    private static readonly ConditionalWeakTable<GodModeValueControl, object?> EnhancedControls = [];
    private static bool _registered;
    private static bool _enabled;

    public void Initialize(IExtensionContext context)
    {
        _enabled = true;
        if (_registered) return;
        _registered = true;

        Application.Current?.Dispatcher.Invoke(() =>
        {
            EventManager.RegisterClassHandler(typeof(GodModeValueControl), FrameworkElement.LoadedEvent,
                new RoutedEventHandler((s, e) =>
                {
                    if (_enabled && s is GodModeValueControl ctrl && !EnhancedControls.TryGetValue(ctrl, out var _))
                    {
                        if (EnhanceControl(ctrl))
                            EnhancedControls.Add(ctrl, null);
                    }
                }));
        });
    }

    private static bool EnhanceControl(GodModeValueControl ctrl)
    {
        if (ctrl.FindName("_slider") is not Slider slider ||
            ctrl.FindName("_sliderLabel") is not Label sliderLabel ||
            slider.Parent is not Grid parentGrid)
            return false;

        var unit = ctrl.Unit ?? string.Empty;
        var format = string.IsNullOrEmpty(unit) ? "{0:0.##}" : $"{{0:0.##}} {unit}";

        // 隐藏旧的右侧独立数值标签（宿主 Set() 载入预设时会改回 Visible，依靠 Width/MaxWidth=0 与 Opacity=0 彻底隐藏）
        sliderLabel.Visibility = Visibility.Collapsed;
        sliderLabel.Width = 0;
        sliderLabel.MaxWidth = 0;
        sliderLabel.MinWidth = 0;
        sliderLabel.Padding = default;
        sliderLabel.Margin = default;
        sliderLabel.Opacity = 0;

        // 从父 Grid 移除 slider，嵌入包含 Min/Max 的响应式 Grid 包装层
        parentGrid.Children.Remove(slider);

        var minText = CreateValueText(TextAlignment.Right, new Thickness(0, 24, 8, 0));
        minText.SetBinding(TextBlock.TextProperty, new Binding(nameof(Slider.Minimum)) { Source = slider, StringFormat = format });

        var maxText = CreateValueText(TextAlignment.Left, new Thickness(8, 24, 0, 0));
        maxText.SetBinding(TextBlock.TextProperty, new Binding(nameof(Slider.Maximum)) { Source = slider, StringFormat = format });

        slider.VerticalAlignment = VerticalAlignment.Center;
        slider.Margin = new Thickness(0, 24, 0, 0);
        slider.ClipToBounds = false;

        var wrapperGrid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            ClipToBounds = false,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            Children = { minText, slider, maxText }
        };

        Grid.SetColumn(minText, 0);
        Grid.SetColumn(slider, 1);
        Grid.SetColumn(maxText, 2);
        Grid.SetColumn(wrapperGrid, 0);

        // 绑定可见性：与原生 slider 同步（遇到 ComboBox 模式时自动折叠）
        wrapperGrid.SetBinding(UIElement.VisibilityProperty, new Binding(nameof(Slider.Visibility)) { Source = slider });

        parentGrid.Children.Insert(0, wrapperGrid);

        // 挂载与重构 Thumb 模板
        void ApplyTemplate()
        {
            if (slider.Template?.FindName("PART_Track", slider) is not Track { Thumb: { } thumb }) return;

            thumb.ClipToBounds = false;
            thumb.Template = CreateThumbTemplate(format);
        }

        slider.Loaded += (_, _) => ApplyTemplate();
        if (slider.IsLoaded) ApplyTemplate();

        return true;
    }

    private static ControlTemplate CreateThumbTemplate(string format)
    {
        var root = new FrameworkElementFactory(typeof(Grid));
        root.SetValue(UIElement.ClipToBoundsProperty, false);
        root.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        root.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);

        // Fluent 风格圆点（先加，绘制在底层）
        var dot = new FrameworkElementFactory(typeof(Border));
        dot.SetValue(Border.WidthProperty, 16.0);
        dot.SetValue(Border.HeightProperty, 16.0);
        dot.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        dot.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        dot.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        dot.SetResourceReference(Border.BackgroundProperty, "AccentFillColorDefaultBrush");

        // 浮层：0×0 Canvas 绝对定位，锚在 Thumb 顶部中心（后加，绘制在圆点之上，尺寸解耦）
        var layer = new FrameworkElementFactory(typeof(Canvas));
        layer.SetValue(FrameworkElement.WidthProperty, 0.0);
        layer.SetValue(FrameworkElement.HeightProperty, 0.0);
        layer.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        layer.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Top);
        layer.SetValue(UIElement.ClipToBoundsProperty, false);

        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetValue(TextBlock.FontSizeProperty, 11.0);
        text.SetValue(TextBlock.FontWeightProperty, FontWeights.Normal);
        text.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Center);
        text.SetValue(FrameworkElement.WidthProperty, 60.0);
        text.SetValue(Canvas.LeftProperty, -30.0); // Width=60，左移一半即居中
        text.SetValue(Canvas.TopProperty, -20.0);  // 完整位于圆点上方
        text.SetValue(FrameworkElement.MarginProperty, default(Thickness));
        text.SetValue(TextBlock.TextWrappingProperty, TextWrapping.NoWrap);
        text.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
        text.SetBinding(TextBlock.TextProperty, new Binding(nameof(Slider.Value))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(Slider), 1),
            StringFormat = format
        });

        layer.AppendChild(text);

        root.AppendChild(dot);
        root.AppendChild(layer);

        return new ControlTemplate(typeof(Thumb)) { VisualTree = root };
    }

    private static TextBlock CreateValueText(TextAlignment align, Thickness margin)
    {
        var tb = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = align == TextAlignment.Right ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            Margin = margin,
            FontSize = 11,
            TextAlignment = align,
            MinWidth = 24,
            Opacity = 0.75
        };
        tb.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        return tb;
    }

    public void Dispose() => _enabled = false;
}
