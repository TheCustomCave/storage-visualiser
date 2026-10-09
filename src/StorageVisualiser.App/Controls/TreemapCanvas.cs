using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using StorageVisualiser.Core.Analysis;
using StorageVisualiser.Core.Formatting;
using StorageVisualiser.Core.Model;
using StorageVisualiser.Core.Settings;
using StorageVisualiser.Core.Treemap;

namespace StorageVisualiser.App.Controls;

public sealed class TreemapCanvas : Control
{
    public static readonly StyledProperty<TreemapItem?> LayoutRootProperty =
        AvaloniaProperty.Register<TreemapCanvas, TreemapItem?>(nameof(LayoutRoot));

    public static readonly StyledProperty<StorageNode?> SelectedNodeProperty =
        AvaloniaProperty.Register<TreemapCanvas, StorageNode?>(nameof(SelectedNode));

    public static readonly StyledProperty<TreemapColorMode> ColorModeProperty =
        AvaloniaProperty.Register<TreemapCanvas, TreemapColorMode>(nameof(ColorMode), TreemapColorMode.DepthRainbow);

    public static readonly StyledProperty<bool> ColorBlindSafeProperty =
        AvaloniaProperty.Register<TreemapCanvas, bool>(nameof(ColorBlindSafe), false);

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

    public TreemapColorMode ColorMode
    {
        get => GetValue(ColorModeProperty);
        set => SetValue(ColorModeProperty, value);
    }

    public bool ColorBlindSafe
    {
        get => GetValue(ColorBlindSafeProperty);
        set => SetValue(ColorBlindSafeProperty, value);
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

    // Neutral folder brushes for FileTypeCategory and FileAge modes
    private static readonly IBrush NeutralFolderContentBrush = new SolidColorBrush(Color.Parse("#F8FAFC"));
    private static readonly IBrush NeutralFolderHeaderBrush = new SolidColorBrush(Color.Parse("#E2E8F0"));

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

    // Color-blind safe depth palette (Okabe-Ito / Tol high-contrast hues)
    private static readonly (IBrush Content, IBrush Header)[] ColorBlindDepthPalette =
    [
        (new SolidColorBrush(Color.Parse("#FDEEE9")), new SolidColorBrush(Color.Parse("#D55E00"))), // Vermilion
        (new SolidColorBrush(Color.Parse("#FEF6E9")), new SolidColorBrush(Color.Parse("#E69F00"))), // Orange
        (new SolidColorBrush(Color.Parse("#FFFEE6")), new SolidColorBrush(Color.Parse("#F0E442"))), // Yellow
        (new SolidColorBrush(Color.Parse("#E6F5F0")), new SolidColorBrush(Color.Parse("#009E73"))), // Bluish Green
        (new SolidColorBrush(Color.Parse("#EDF7FD")), new SolidColorBrush(Color.Parse("#56B4E9"))), // Sky Blue
        (new SolidColorBrush(Color.Parse("#E6F1F8")), new SolidColorBrush(Color.Parse("#0072B2"))), // Blue
        (new SolidColorBrush(Color.Parse("#F9EEF4")), new SolidColorBrush(Color.Parse("#CC79A7")))  // Reddish Purple
    ];

    // SpaceMonger warm peach/salmon tile color for files
    private static readonly IBrush FileFillBrush = new SolidColorBrush(Color.Parse("#FFCCBC"));
    private static readonly IBrush FileFillSafeBrush = new SolidColorBrush(Color.Parse("#FED7AA"));

    // File type category brushes (standard pastel palette)
    private static readonly Dictionary<string, IBrush> CategoryBrushes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Video"] = new SolidColorBrush(Color.Parse("#DDD6FE")),              // Soft Purple
        ["Images"] = new SolidColorBrush(Color.Parse("#A7F3D0")),             // Soft Emerald
        ["Audio"] = new SolidColorBrush(Color.Parse("#A5F3FC")),              // Soft Cyan
        ["Archives"] = new SolidColorBrush(Color.Parse("#FDE68A")),           // Soft Amber
        ["Documents"] = new SolidColorBrush(Color.Parse("#BFDBFE")),          // Soft Blue
        ["System / Binaries"] = new SolidColorBrush(Color.Parse("#FECACA")),  // Soft Rose
        ["Disk Images / VMs"] = new SolidColorBrush(Color.Parse("#C7D2FE")),  // Soft Indigo
        ["Code / Data"] = new SolidColorBrush(Color.Parse("#99F6E4")),        // Soft Teal
        ["No Extension"] = new SolidColorBrush(Color.Parse("#E2E8F0")),       // Light Slate
        ["Other"] = new SolidColorBrush(Color.Parse("#CBD5E1"))               // Slate
    };

    // Color-blind safe category brushes (Okabe-Ito high-contrast tints)
    private static readonly Dictionary<string, IBrush> ColorBlindCategoryBrushes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Video"] = new SolidColorBrush(Color.Parse("#F3E8FF")),              // Purple tint
        ["Images"] = new SolidColorBrush(Color.Parse("#CCFBF1")),             // Teal tint
        ["Audio"] = new SolidColorBrush(Color.Parse("#BAE6FD")),              // Sky Blue tint
        ["Archives"] = new SolidColorBrush(Color.Parse("#FEF3C7")),           // Amber tint
        ["Documents"] = new SolidColorBrush(Color.Parse("#DBEAFE")),          // Blue tint
        ["System / Binaries"] = new SolidColorBrush(Color.Parse("#FFE4E6")),  // Vermilion/Rose tint
        ["Disk Images / VMs"] = new SolidColorBrush(Color.Parse("#E0E7FF")),  // Indigo tint
        ["Code / Data"] = new SolidColorBrush(Color.Parse("#FEF9C3")),        // Yellow tint
        ["No Extension"] = new SolidColorBrush(Color.Parse("#F1F5F9")),       // Neutral slate
        ["Other"] = new SolidColorBrush(Color.Parse("#E2E8F0"))               // Neutral slate
    };

    // File age brushes (standard recency tiers)
    private static readonly IBrush AgeRecentBrush = new SolidColorBrush(Color.Parse("#BAE6FD"));      // < 1 month: Fresh blue
    private static readonly IBrush AgeMediumRecentBrush = new SolidColorBrush(Color.Parse("#BBF7D0"));// 1-6 months: Green
    private static readonly IBrush AgeAgingBrush = new SolidColorBrush(Color.Parse("#FEF08A"));       // 6-12 months: Yellow
    private static readonly IBrush AgeOldBrush = new SolidColorBrush(Color.Parse("#FED7AA"));         // 1-2 years: Orange
    private static readonly IBrush AgeArchivedBrush = new SolidColorBrush(Color.Parse("#CBD5E1"));    // > 2 years: Muted slate

    // Color-blind safe file age brushes (monotonic luminance/blue-yellow gradient)
    private static readonly IBrush AgeSafeRecentBrush = new SolidColorBrush(Color.Parse("#E0F2FE"));      // Lightest blue
    private static readonly IBrush AgeSafeMediumRecentBrush = new SolidColorBrush(Color.Parse("#BAE6FD"));// Medium blue
    private static readonly IBrush AgeSafeAgingBrush = new SolidColorBrush(Color.Parse("#FEF3C7"));       // Light yellow
    private static readonly IBrush AgeSafeOldBrush = new SolidColorBrush(Color.Parse("#FDE68A"));         // Deeper yellow/amber
    private static readonly IBrush AgeSafeArchivedBrush = new SolidColorBrush(Color.Parse("#CBD5E1"));    // Slate

    static TreemapCanvas()
    {
        AffectsRender<TreemapCanvas>(LayoutRootProperty, SelectedNodeProperty, ColorModeProperty, ColorBlindSafeProperty);
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
            var (bgBrush, headerBrush) = GetFolderBrushes(item, node.Kind == StorageItemKind.OtherGroup);

            // Outer folder background
            context.FillRectangle(bgBrush, rect);
            context.DrawRectangle(BorderPen, rect);

            // Folder Header bar
            if (!item.HeaderBounds.IsEmpty)
            {
                var h = item.HeaderBounds;
                var headerRect = new Rect(h.X, h.Y, h.Width, h.Height);
                context.FillRectangle(headerBrush, headerRect);
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
            var fileBrush = GetFileBrush(node);
            context.FillRectangle(fileBrush, rect);
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

    private (IBrush Content, IBrush Header) GetFolderBrushes(TreemapItem item, bool isOtherGroup)
    {
        if (isOtherGroup)
        {
            return (OtherGroupBrush, OtherGroupBrush);
        }

        if (ColorMode == TreemapColorMode.DepthRainbow)
        {
            var palette = ColorBlindSafe ? ColorBlindDepthPalette : DepthPalette;
            return palette[item.Depth % palette.Length];
        }

        // For FileTypeCategory and FileAge modes, folder shells are calm neutral so the file color tiles pop out
        return (NeutralFolderContentBrush, NeutralFolderHeaderBrush);
    }

    private IBrush GetFileBrush(StorageNode node)
    {
        return ColorMode switch
        {
            TreemapColorMode.FileTypeCategory => GetFileTypeBrush(node),
            TreemapColorMode.FileAge => GetFileAgeBrush(node),
            _ => ColorBlindSafe ? FileFillSafeBrush : FileFillBrush
        };
    }

    private IBrush GetFileTypeBrush(StorageNode node)
    {
        var ext = Path.GetExtension(node.Name);
        var category = StorageAnalysisEngine.CategorizeExtension(ext);
        var brushMap = ColorBlindSafe ? ColorBlindCategoryBrushes : CategoryBrushes;
        if (brushMap.TryGetValue(category, out var brush))
        {
            return brush;
        }
        return brushMap["Other"];
    }

    private IBrush GetFileAgeBrush(StorageNode node)
    {
        if (!node.LastModified.HasValue)
        {
            return ColorBlindSafe ? AgeSafeArchivedBrush : AgeArchivedBrush;
        }

        var age = DateTimeOffset.Now - node.LastModified.Value;
        if (age < TimeSpan.FromDays(30))
        {
            return ColorBlindSafe ? AgeSafeRecentBrush : AgeRecentBrush;
        }
        if (age < TimeSpan.FromDays(180))
        {
            return ColorBlindSafe ? AgeSafeMediumRecentBrush : AgeMediumRecentBrush;
        }
        if (age < TimeSpan.FromDays(365))
        {
            return ColorBlindSafe ? AgeSafeAgingBrush : AgeAgingBrush;
        }
        if (age < TimeSpan.FromDays(730))
        {
            return ColorBlindSafe ? AgeSafeOldBrush : AgeOldBrush;
        }
        return ColorBlindSafe ? AgeSafeArchivedBrush : AgeArchivedBrush;
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
