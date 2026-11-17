using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using TricksterMap.Data;

namespace TricksterMap
{
    /// <summary>One raster pixel per stored cell, scaled by the viewport, cached between edits.</summary>
    internal sealed class CollisionGridImage : IDisposable
    {
        private readonly ConfigLayer layer;
        private Bitmap bitmap;
        private bool dirty = true;
        public CollisionGridImage(ConfigLayer layer) { this.layer = layer; }
        public void Invalidate() { dirty = true; }
        public void Draw(Graphics graphics, RectangleF destination, float opacity)
        {
            if (bitmap == null || dirty) Rebuild();
            using (var attributes = new ImageAttributes())
            {
                attributes.SetColorMatrix(new ColorMatrix { Matrix33 = opacity });
                var points = new[] { new PointF(destination.Left, destination.Top),
                    new PointF(destination.Right, destination.Top), new PointF(destination.Left, destination.Bottom) };
                graphics.DrawImage(bitmap, points, new RectangleF(0, 0, bitmap.Width, bitmap.Height),
                    GraphicsUnit.Pixel, attributes);
            }
        }
        private void Rebuild()
        {
            if (bitmap == null) bitmap = new Bitmap(layer.X, layer.Y, PixelFormat.Format32bppArgb);
            var bits = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                var row = new int[layer.X];
                for (int y = 0; y < layer.Y; y++)
                {
                    for (int x = 0; x < layer.X; x++)
                    {
                        byte value = layer.Data[y * layer.X + x];
                        row[x] = (value == 0 ? Color.PaleGreen : value == 1 ? Color.DarkSlateGray :
                            value == 2 ? Color.Orange : value == 3 ? Color.MediumPurple : Color.HotPink).ToArgb();
                    }
                    Marshal.Copy(row, 0, IntPtr.Add(bits.Scan0, y * bits.Stride), row.Length);
                }
            }
            finally { bitmap.UnlockBits(bits); }
            dirty = false;
        }
        public void Dispose() { bitmap?.Dispose(); bitmap = null; }
    }
}
