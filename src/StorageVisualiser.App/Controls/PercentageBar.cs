using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace StorageVisualiser.App.Controls;

public sealed class PercentageBar : Control
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<PercentageBar, double>(nameof(Value), 0.0);

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<PercentageBar, string>(nameof(Text), string.Empty);

    public static readonly StyledProperty<IBrush> FillBrushProperty =
        AvaloniaProperty.Register<PercentageBar, IBrush>(nameof(FillBrush), new SolidColorBrush(Color.Parse("#93C5FD")));

    public static readonly StyledProperty<IBrush> TrackBrushProperty =
        AvaloniaProperty.Register<PercentageBar, IBrush>(nameof(TrackBrush), new SolidColorBrush(Color.Parse("#F1F5F9")));

    public static readonly StyledProperty<IBrush> BorderBrushProperty =
        AvaloniaProperty.Register<PercentageBar, IBrush>(nameof(BorderBrush), new SolidColorBrush(Color.Parse("#CBD5E1")));

    public static readonly StyledProperty<IBrush> TextBrushProperty =
        AvaloniaProperty.Register<PercentageBar, IBrush>(nameof(TextBrush), new SolidColorBrush(Color.Parse("#0F172A")));

    public static readonly StyledProperty<double> CornerRadiusProperty =
        AvaloniaProperty.Register<PercentageBar, double>(nameof(CornerRadius), 3.0);

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public IBrush FillBrush
    {
        get => GetValue(FillBrushProperty);
        set => SetValue(FillBrushProperty, value);
    }

    public IBrush TrackBrush
    {
        get => GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public IBrush BorderBrush
    {
        get => GetValue(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    public IBrush TextBrush
    {
        get => GetValue(TextBrushProperty);
        set => SetValue(TextBrushProperty, value);
    }

    public double CornerRadius
    {
        get => GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    private static readonly Typeface BoldTypeface = new(FontFamily.Default, FontStyle.Normal, FontWeight.Bold);

    static PercentageBar()
    {
        AffectsRender<PercentageBar>(ValueProperty, TextProperty, FillBrushProperty, TrackBrushProperty, BorderBrushProperty, TextBrushProperty, CornerRadiusProperty);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w <= 0 || h <= 0) return;

        var r = CornerRadius;
        var rect = new Rect(0, 0, w, h);
        var roundedRect = new RoundedRect(rect, r);

        // 1. Draw track background
        context.FillRectangle(TrackBrush, rect);

        // 2. Draw fill clipped to pill bounds
        using (context.PushClip(roundedRect))
        {
            var pct = Math.Clamp(Value / 100.0, 0.0, 1.0);
            var fillW = w * pct;
            if (fillW > 0)
            {
                context.FillRectangle(FillBrush, new Rect(0, 0, fillW, h));
            }

            // 3. Draw text centered
            var text = Text;
            if (!string.IsNullOrEmpty(text))
            {
                var fontSize = Math.Max(8.5, Math.Min(10.5, h - 5.0));
                var ft = new FormattedText(
                    text,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    BoldTypeface,
                    fontSize,
                    TextBrush);

                var tx = Math.Max(2.0, (w - ft.Width) / 2.0);
                var ty = Math.Max(0.0, (h - ft.Height) / 2.0);
                context.DrawText(ft, new Point(tx, ty));
            }
        }

        // 4. Draw outer border
        context.DrawRectangle(new Pen(BorderBrush, 1), rect);
    }
}
