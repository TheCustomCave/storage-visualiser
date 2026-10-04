using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using StorageVisualiser.Core.Formatting;
using StorageVisualiser.Core.Model;
using StorageVisualiser.Core.Treemap;

namespace StorageVisualiser.App.Controls;

public sealed class TreemapCanvas : Control
{
    public static readonly StyledProperty<TreemapItem?> LayoutRootProperty =
        AvaloniaProperty.Register<TreemapCanvas, TreemapItem?>(nameof(LayoutRoot));

    public static readonly StyledProperty<StorageNode?> SelectedNodeProperty =
        AvaloniaProperty.Register<TreemapCanvas, StorageNode?>(nameof(SelectedNode));

    public TreemapItem? LayoutRoot
    {
        get => GetValue(LayoutRootProperty);
        set => SetValue(LayoutRootProperty, value);
    }

    public StorageNode? SelectedNode
    {
        get => GetValue(SelectedNodeProperty);
        set => SetValue(SelectedNodeProperty, value);
    }

    public event Action<StorageNode?>? NodeSelected;
    public event Action<StorageNode>? NodeDrillDown;
    public event Action<double, double>? SizeChangedAction;

    private static readonly IBrush FreeSpaceBrush = new SolidColorBrush(Color.Parse("#F2F4F4"));
    private static readonly IBrush FreeSpaceHeaderBrush = new SolidColorBrush(Color.Parse("#CFD8DC"));
    private static readonly IBrush OtherGroupBrush = new SolidColorBrush(Color.Parse("#EEEEEE"));
    private static readonly IBrush InaccessibleBrush = new SolidColorBrush(Color.Parse("#FFCDD2"));
    private static readonly IPen BorderPen = new Pen(new SolidColorBrush(Color.Parse("#333333")), 0.75);
    private static readonly IPen SelectionPen = new Pen(new SolidColorBrush(Color.Parse("#0078D4")), 2.0);
    private static readonly IBrush SelectionOverlayBrush = new SolidColorBrush(Color.FromArgb(40, 0, 120, 212));
    private static readonly IBrush TextBrush = new SolidColorBrush(Color.Parse("#111111"));
    private static readonly Typeface DefaultTypeface = new("Segoe UI", FontStyle.Normal, FontWeight.Normal);
    private static readonly Typeface BoldTypeface = new("Segoe UI", FontStyle.Normal, FontWeight.SemiBold);

    private static readonly (IBrush Content, IBrush Header)[] DepthPalette =
    [
        (new SolidColorBrush(Color.Parse("#FFF9C4")), new SolidColorBrush(Color.Parse("#FBC02D"))), // Gold / Yellow
        (new SolidColorBrush(Color.Parse("#C8E6C9")), new SolidColorBrush(Color.Parse("#4CAF50"))), // Green
        (new SolidColorBrush(Color.Parse("#B3E5FC")), new SolidColorBrush(Color.Parse("#03A9F4"))), // Light Blue
        (new SolidColorBrush(Color.Parse("#E1BEE7")), new SolidColorBrush(Color.Parse("#9C27B0"))), // Purple
        (new SolidColorBrush(Color.Parse("#FFCCBC")), new SolidColorBrush(Color.Parse("#FF5722"))), // Coral
        (new SolidColorBrush(Color.Parse("#B2DFDB")), new SolidColorBrush(Color.Parse("#009688"))), // Teal
        (new SolidColorBrush(Color.Parse("#D1C4E9")), new SolidColorBrush(Color.Parse("#673AB7")))  // Deep Purple
    ];

    private static readonly IBrush FileFillBrush = new SolidColorBrush(Color.Parse("#E3F2FD"));

    static TreemapCanvas()
    {
        AffectsRender<TreemapCanvas>(LayoutRootProperty, SelectedNodeProperty);
    }

    public TreemapCanvas()
    {
        ClipToBounds = true;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (finalSize.Width > 0 && finalSize.Height > 0)
        {
            SizeChangedAction?.Invoke(finalSize.Width, finalSize.Height);
        }
        return base.ArrangeOverride(finalSize);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var pt = e.GetPosition(this);
        var hit = LayoutRoot?.HitTest(pt.X, pt.Y);
        NodeSelected?.Invoke(hit?.Node);
    }

    protected override void OnDoubleTapped(TappedEventArgs e)
    {
        base.OnDoubleTapped(e);
        var pt = e.GetPosition(this);
        var hit = LayoutRoot?.HitTest(pt.X, pt.Y);
        if (hit?.Node != null && hit.Node.Kind == StorageItemKind.Directory)
        {
            NodeDrillDown?.Invoke(hit.Node);
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (LayoutRoot == null || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        RenderItem(context, LayoutRoot);
    }

    private void RenderItem(DrawingContext context, TreemapItem item)
    {
        var b = item.Bounds;
        if (b.Width < 1 || b.Height < 1) return;

        var rect = new Rect(b.X, b.Y, b.Width, b.Height);
        var node = item.Node;

        if (node.Kind == StorageItemKind.Directory)
        {
            var palette = DepthPalette[item.Depth % DepthPalette.Length];

            // Outer folder background
            context.FillRectangle(palette.Content, rect);
            context.DrawRectangle(BorderPen, rect);

            // Folder Header bar
            if (!item.HeaderBounds.IsEmpty)
            {
                var h = item.HeaderBounds;
                var headerRect = new Rect(h.X, h.Y, h.Width, h.Height);
                context.FillRectangle(palette.Header, headerRect);
                context.DrawRectangle(BorderPen, headerRect);

                if (h.Width >= 20 && h.Height >= 10)
                {
                    var title = $"{node.Name} ({SizeFormatter.Format(item.EffectiveSize)})";
                    var ft = new FormattedText(
                        title,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        BoldTypeface,
                        Math.Max(9, Math.Min(11, h.Height - 4)),
                        TextBrush)
                    {
                        MaxTextWidth = Math.Max(0, h.Width - 6),
                        MaxTextHeight = h.Height
                    };
                    context.DrawText(ft, new Point(h.X + 3, h.Y + (h.Height - ft.Height) / 2));
                }
            }

            // Children inside content bounds
            if (item.HasChildren)
            {
                foreach (var child in item.Children)
                {
                    RenderItem(context, child);
                }
            }
        }
        else if (node.Kind == StorageItemKind.DriveFreeSpace)
        {
            context.FillRectangle(FreeSpaceBrush, rect);
            context.DrawRectangle(BorderPen, rect);

            if (b.Width >= 40 && b.Height >= 25)
            {
                DrawBlockText(context, "<Free Space>", SizeFormatter.Format(node.Size), null, rect);
            }
        }
        else if (node.Kind == StorageItemKind.OtherGroup)
        {
            context.FillRectangle(OtherGroupBrush, rect);
            context.DrawRectangle(BorderPen, rect);

            if (b.Width >= 30 && b.Height >= 20)
            {
                DrawBlockText(context, node.Name, SizeFormatter.Format(node.Size), null, rect);
            }
        }
        else if (node.Kind == StorageItemKind.Inaccessible)
        {
            context.FillRectangle(InaccessibleBrush, rect);
            context.DrawRectangle(BorderPen, rect);
        }
        else
        {
            // Standard File
            context.FillRectangle(FileFillBrush, rect);
            context.DrawRectangle(BorderPen, rect);

            if (b.Width >= 24 && b.Height >= 14)
            {
                var dateStr = b.Height >= 45 ? node.LastModified?.LocalDateTime.ToString("dd MMM yyyy", CultureInfo.CurrentCulture) : null;
                DrawBlockText(context, node.Name, SizeFormatter.Format(node.Size), dateStr, rect);
            }
        }

        // Selection Highlight
        if (SelectedNode != null && ReferenceEquals(SelectedNode, node))
        {
            context.DrawRectangle(SelectionPen, rect);
            context.FillRectangle(SelectionOverlayBrush, rect);
        }
    }

    private static void DrawBlockText(DrawingContext context, string title, string? subtitle, string? extra, Rect rect)
    {
        var padX = 3.0;
        var padY = 2.0;
        var availW = Math.Max(0, rect.Width - padX * 2);
        var availH = Math.Max(0, rect.Height - padY * 2);

        if (availW < 15 || availH < 10) return;

        double fontSize = availH >= 40 ? 11 : 9.5;

        // Line 1: Title
        var ftTitle = new FormattedText(
            title,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            DefaultTypeface,
            fontSize,
            TextBrush)
        {
            MaxTextWidth = availW,
            MaxTextHeight = availH
        };

        var curY = rect.Y + padY;
        context.DrawText(ftTitle, new Point(rect.X + padX, curY));
        curY += ftTitle.Height;

        // Line 2: Size
        if (subtitle != null && curY + 10 <= rect.Bottom - padY)
        {
            var ftSub = new FormattedText(
                subtitle,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                DefaultTypeface,
                Math.Max(8.5, fontSize - 1.5),
                TextBrush)
            {
                MaxTextWidth = availW,
                MaxTextHeight = Math.Max(0, rect.Bottom - curY - padY)
            };
            context.DrawText(ftSub, new Point(rect.X + padX, curY));
            curY += ftSub.Height;
        }

        // Line 3: Extra (Date)
        if (extra != null && curY + 10 <= rect.Bottom - padY)
        {
            var ftExtra = new FormattedText(
                extra,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                DefaultTypeface,
                Math.Max(8.0, fontSize - 2.0),
                TextBrush)
            {
                MaxTextWidth = availW,
                MaxTextHeight = Math.Max(0, rect.Bottom - curY - padY)
            };
            context.DrawText(ftExtra, new Point(rect.X + padX, curY));
        }
    }
}
