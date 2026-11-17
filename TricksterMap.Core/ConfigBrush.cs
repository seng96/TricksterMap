using System;

namespace TricksterMap
{
    /// <summary>Shared cell-center rasterization for painting and the viewport footprint.</summary>
    public static class ConfigBrush
    {
        public const int MaxSize = 128;

        public static void ValidateSize(int size)
        {
            if (size < 1 || size > MaxSize)
                throw new ArgumentOutOfRangeException(nameof(size), "Brush diameter must be 1..128 cells.");
        }

        public static bool Contains(int dx, int dy, int size, bool circle)
        {
            ValidateSize(size);
            int left = -size / 2;
            int x = dx - left, y = dy - left;
            if (x < 0 || y < 0 || x >= size || y >= size) return false;
            if (!circle) return true;
            int cx = 2 * x + 1 - size, cy = 2 * y + 1 - size;
            return cx * cx + cy * cy <= size * size;
        }

        public static void Visit(int centerX, int centerY, int size, bool circle,
            int width, int height, Action<int, int> visit)
        {
            ValidateSize(size);
            int left = centerX - size / 2, top = centerY - size / 2;
            int x0 = Math.Max(0, left), y0 = Math.Max(0, top);
            int x1 = Math.Min(width, left + size), y1 = Math.Min(height, top + size);
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                    if (Contains(x - centerX, y - centerY, size, circle)) visit(x, y);
        }
    }
}
