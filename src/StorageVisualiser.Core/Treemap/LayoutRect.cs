using System;

namespace StorageVisualiser.Core.Treemap;

public readonly record struct LayoutRect(double X, double Y, double Width, double Height)
{
    public static LayoutRect Empty => new(0, 0, 0, 0);

    public double Right => X + Width;
    public double Bottom => Y + Height;
    public double Area => Math.Max(0, Width) * Math.Max(0, Height);
    public bool IsEmpty => Width <= 0 || Height <= 0;

    public bool Contains(double px, double py) =>
        px >= X && px <= Right && py >= Y && py <= Bottom;

    public LayoutRect Deflate(double padding) =>
        new(X + padding, Y + padding, Math.Max(0, Width - padding * 2), Math.Max(0, Height - padding * 2));
}
