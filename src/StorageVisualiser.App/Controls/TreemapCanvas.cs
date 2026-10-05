using System;
using System.Collections.Generic;
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

    // SpaceMonger classic palette
    private static readonly IBrush FreeSpaceBrush = new SolidColorBrush(Color.Parse("#ECECEC"));
    private static readonly IBrush OtherGroupBrush = new SolidColorBrush(Color.Parse("#E0E0E0"));
    private static readonly IBrush InaccessibleBrush = new SolidColorBrush(Color.Parse("#FFCDD2"));
    private static readonly IPen BorderPen = new Pen(new SolidColorBrush(Color.Parse("#222222")), 1.0);
    private static readonly IPen SelectionPen = new Pen(new SolidColorBrush(Color.Parse("#0078D4")), 2.5);
    private static readonly IBrush SelectionOverlayBrush = new SolidColorBrush(Color.FromArgb(45, 0, 120, 212));
    private static readonly IBrush TextBrush = new SolidColorBrush(Color.Parse("#111111"));
    private static readonly Typeface DefaultTypeface = new("Segoe UI", FontStyle.Normal, FontWeight.Normal);
    private static readonly Typeface BoldTypeface = new("Segoe UI", FontStyle.Normal, FontWeight.SemiBold);

    // SpaceMonger rainbow depth levels
    private static readonly (IBrush Content, IBrush Header)[] DepthPalette =
    [
        // Depth 0: Salmon / Coral Red
        (new SolidColorBrush(Color.Parse("#FFEBE8")), new SolidColorBrush(Color.Parse("#FF7060"))),
        // Depth 1: Soft Canary Yellow
        (new SolidColorBrush(Color.Parse("#FFFDE7")), new SolidColorBrush(Color.Parse("#FFE040"))),
        // Depth 2: Mint / Spring Green
        (new SolidColorBrush(Color.Parse("#E8F5E9")), new SolidColorBrush(Color.Parse("#76D275"))),
        // Depth 3: Sky Blue / Cyan
        (new SolidColorBrush(Color.Parse("#E1F5FE")), new SolidColorBrush(Color.Parse("#40C4FF"))),
        // Depth 4: Lavender / Soft Purple
        (new SolidColorBrush(Color.Parse("#F3E5F5")), new SolidColorBrush(Color.Parse("#BA68C8"))),
        // Depth 5: Warm Coral / Peach
        (new SolidColorBrush(Color.Parse("#FBE9E7")), new SolidColorBrush(Color.Parse("#FF8A65"))),
        // Depth 6: Aqua / Teal
        (new SolidColorBrush(Color.Parse("#E0F2F1")), new SolidColorBrush(Color.Parse("#4DB6AC")))
    ];

    // SpaceMonger warm peach/salmon tile color for files
    private static readonly IBrush FileFillBrush = new SolidColorBrush(Color.Parse("#FFCCBC"));

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
        if (hit?.Node != null && (hit.Node.Kind == StorageItemKind.Directory || (hit.Node.Kind == StorageItemKind.OtherGroup && hit.Node.HasChildren)))
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

        if (node.Kind == StorageItemKind.Directory || item.HasChildren)
        {
            var palette = DepthPalette[item.Depth % DepthPalette.Length];
            var bgBrush = node.Kind == StorageItemKind.OtherGroup ? OtherGroupBrush : palette.Content;

            // Outer folder background
            context.FillRectangle(bgBrush, rect);
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
                        Math.Max(9.5, Math.Min(11.0, h.Height - 5)),
                        TextBrush)
                    {
                        MaxTextWidth = Math.Max(0, h.Width - 6),
                        MaxTextHeight = h.Height
                    };
                    context.DrawText(ft, new Point(h.X + 4, h.Y + (h.Height - ft.Height) / 2));
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
            else if (item.HeaderBounds.IsEmpty && b.Width >= 24 && b.Height >= 14)
            {
                // Atomic leaf folder without separate header
                DrawBlockText(context, node.Name, SizeFormatter.Format(node.Size), null, rect);
            }
        }
        else if (node.Kind == StorageItemKind.DriveFreeSpace)
        {
            context.FillRectangle(FreeSpaceBrush, rect);
            context.DrawRectangle(BorderPen, rect);

            if (b.Width >= 60 && b.Height >= 35)
            {
                double freePct = 0;
                long totalFiles = 0;
                long totalDirs = 0;

                if (LayoutRoot?.Node != null)
                {
                    totalFiles = LayoutRoot.Node.FileCount;
                    totalDirs = LayoutRoot.Node.DirectoryCount;
                    long totalCapacity = LayoutRoot.Node.Size + node.Size;
                    if (totalCapacity > 0)
                    {
                        freePct = (double)node.Size / totalCapacity * 100.0;
                    }
                }

                DrawFreeSpaceText(context, freePct, node.Size, totalFiles, totalDirs, rect);
            }
        }
        else if (node.Kind == StorageItemKind.OtherGroup)
        {
            context.FillRectangle(OtherGroupBrush, rect);
            context.DrawRectangle(BorderPen, rect);

            if (b.Width >= 26 && b.Height >= 14)
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

    private static void DrawFreeSpaceText(
        DrawingContext context,
        double freePct,
        long freeBytes,
        long totalFiles,
        long totalDirs,
        Rect rect)
    {
        var lines = new List<string>
        {
            freePct > 0 ? $"<Free Space: {freePct:F1}%>" : "<Free Space>",
            $"{SizeFormatter.Format(freeBytes)} Free"
        };

        if (totalFiles > 0 || totalDirs > 0)
        {
            lines.Add($"Files Total: {totalFiles:N0}");
            lines.Add($"Folders Total: {totalDirs:N0}");
        }

        double lineHeight = 18.0;
        double totalH = lines.Count * lineHeight;
        if (rect.Height < totalH + 10 || rect.Width < 110)
        {
            lines = [lines[0], lines[1]];
            totalH = lines.Count * lineHeight;
            if (rect.Height < totalH) return;
        }

        double startY = rect.Y + (rect.Height - totalH) / 2.0;

        for (int i = 0; i < lines.Count; i++)
        {
            var isBold = i == 0;
            var tf = isBold ? BoldTypeface : DefaultTypeface;
            var fontSize = isBold ? 12.0 : 10.5;

            var ft = new FormattedText(
                lines[i],
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                tf,
                fontSize,
                TextBrush)
            {
                MaxTextWidth = rect.Width - 10,
                TextAlignment = TextAlignment.Center
            };

            context.DrawText(ft, new Point(rect.X + 5, startY + (i * lineHeight)));
        }
    }

    private static void DrawBlockText(DrawingContext context, string title, string? subtitle, string? extra, Rect rect)
    {
        var padX = 4.0;
        var padY = 2.0;
        var availW = Math.Max(0, rect.Width - padX * 2);
        var availH = Math.Max(0, rect.Height - padY * 2);

        // Do not render text if box is too small (prevents ... dot clutter)
        if (availW < 24 || availH < 14) return;

        double fontSize = availH >= 45 ? 10.5 : 9.0;

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
        if (subtitle != null && curY + 11 <= rect.Bottom - padY)
        {
            var ftSub = new FormattedText(
                subtitle,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                DefaultTypeface,
                Math.Max(8.0, fontSize - 1.5),
                TextBrush)
            {
                MaxTextWidth = availW,
                MaxTextHeight = Math.Max(0, rect.Bottom - curY - padY)
            };
            context.DrawText(ftSub, new Point(rect.X + padX, curY));
            curY += ftSub.Height;
        }

        // Line 3: Extra (Date)
        if (extra != null && curY + 11 <= rect.Bottom - padY)
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
