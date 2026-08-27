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
/// - 重构 Thumb 控件模板，利用 WPF 原生数据绑定将当前数值同轴居中悬浮于圆点正上方
/// </summary>
public sealed class SliderEnhancer : IUiFeature
{
    private static readonly ConditionalWeakTable<GodModeValueControl, object?> EnhancedControls = [];

    public void Initialize(IExtensionContext context)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            EventManager.RegisterClassHandler(typeof(GodModeValueControl), FrameworkElement.LoadedEvent,
                new RoutedEventHandler((s, e) =>
                {
                    if (s is GodModeValueControl ctrl && !EnhancedControls.TryGetValue(ctrl, out _))
                    {
                        EnhancedControls.Add(ctrl, null);
                        EnhanceControl(ctrl);
                    }
                }));
        });
    }

    private static void EnhanceControl(GodModeValueControl ctrl)
    {
        if (ctrl.FindName("_slider") is not Slider slider ||
            ctrl.FindName("_sliderLabel") is not Label sliderLabel ||
            slider.Parent is not Grid parentGrid)
            return;

        var unit = ctrl.Unit ?? string.Empty;
        var format = string.IsNullOrEmpty(unit) ? "{0:0.##}" : $"{{0:0.##}} {unit}";

        // 隐藏旧的右侧独立数值标签
        sliderLabel.Visibility = Visibility.Collapsed;
        sliderLabel.Width = 0;
        sliderLabel.Margin = default;

        // 从父 Grid 移除 slider，嵌入包含 Min/Max 的响应式 Grid 包装层
        parentGrid.Children.Remove(slider);

        var minText = CreateValueText(TextAlignment.Right, new Thickness(0, 16, 8, 0));
        minText.SetBinding(TextBlock.TextProperty, new Binding(nameof(Slider.Minimum)) { Source = slider, StringFormat = format });

        var maxText = CreateValueText(TextAlignment.Left, new Thickness(8, 16, 0, 0));
        maxText.SetBinding(TextBlock.TextProperty, new Binding(nameof(Slider.Maximum)) { Source = slider, StringFormat = format });

        slider.VerticalAlignment = VerticalAlignment.Center;
        slider.Margin = new Thickness(0, 16, 0, 0);
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
    }

    private static ControlTemplate CreateThumbTemplate(string format)
    {
        var root = new FrameworkElementFactory(typeof(Grid));
        root.SetValue(UIElement.ClipToBoundsProperty, false);
        root.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        root.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);

        // 悬浮纯文本：60px 宽度对称负边距，常规字重主色调，直接绑定当前 Slider.Value
        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetValue(TextBlock.FontSizeProperty, 11.0);
        text.SetValue(TextBlock.FontWeightProperty, FontWeights.Normal);
        text.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Center);
        text.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        text.SetValue(FrameworkElement.WidthProperty, 60.0);
        text.SetValue(FrameworkElement.MarginProperty, new Thickness(-22, -20, -22, 0));
        text.SetValue(TextBlock.TextWrappingProperty, TextWrapping.NoWrap);
        text.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
        text.SetBinding(TextBlock.TextProperty, new Binding(nameof(Slider.Value))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(Slider), 1),
            StringFormat = format
        });

        // Fluent 风格圆点
        var dot = new FrameworkElementFactory(typeof(Border));
        dot.SetValue(Border.WidthProperty, 16.0);
        dot.SetValue(Border.HeightProperty, 16.0);
        dot.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        dot.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        dot.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        dot.SetResourceReference(Border.BackgroundProperty, "AccentFillColorDefaultBrush");

        root.AppendChild(text);
        root.AppendChild(dot);

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
}
