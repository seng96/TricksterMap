using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace TricksterMap
{
    /// <summary>Exact discrete footprint from the same cell-center mask as the brush service.</summary>
    internal sealed class ViewportBrushFootprint : IDisposable
    {
        private Bitmap bitmap;
        private int diameter;
        private bool circle;

        public void Draw(Graphics graphics, RectangleF bounds, RectangleF mapBounds, int size, bool round)
        {
            if (bitmap == null || diameter != size || circle != round) Rebuild(size, round);
            var saved = graphics.Save();
            try
            {
                graphics.SetClip(mapBounds, CombineMode.Intersect);
                graphics.DrawImage(bitmap, new[] { new PointF(bounds.Left, bounds.Top),
                    new PointF(bounds.Right, bounds.Top), new PointF(bounds.Left, bounds.Bottom) },
                    new RectangleF(0, 0, size, size), GraphicsUnit.Pixel);
            }
            finally { graphics.Restore(saved); }
        }

        private void Rebuild(int size, bool round)
        {
            bitmap?.Dispose(); diameter = size; circle = round;
            bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            var pixels = new int[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    if (ConfigBrush.Contains(x - size / 2, y - size / 2, size, round))
                        pixels[y * size + x] = Color.FromArgb(72, Color.Yellow).ToArgb();
            var bits = bitmap.LockBits(new Rectangle(0, 0, size, size), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try { Marshal.Copy(pixels, 0, bits.Scan0, pixels.Length); }
            finally { bitmap.UnlockBits(bits); }
        }

        public void Dispose() { bitmap?.Dispose(); bitmap = null; }
    }
}
