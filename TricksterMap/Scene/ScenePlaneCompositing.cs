using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace TricksterMap.SceneView
{
    public sealed partial class SceneRenderer
    {
        private void RenderBuffered(Graphics graphics, Rectangle viewport, double zoom, double originX, double originY,
            SceneObjectMovePreview move, bool drawEffects)
        {
            int rows = Math.Max(1, 4194304 / viewport.Width);
            for (int y = viewport.Top; y < viewport.Bottom; y += rows)
            {
                int height = Math.Min(rows, viewport.Bottom - y);
                using (var surface = new Bitmap(viewport.Width, height, PixelFormat.Format32bppArgb))
                {
                    double left = originX - viewport.Left, top = originY - y;
                    using (var buffer = Graphics.FromImage(surface))
                        RenderBase(buffer, new Rectangle(0, 0, surface.Width, surface.Height), zoom, left, top, move, surface);
                    if (drawEffects)
                        effects.Render(surface, zoom, left, top, AnimateObjects ? AnimationTimeSeconds : 1);
                    graphics.DrawImageUnscaled(surface, viewport.Left, y);
                }
            }
        }

        private void CompositePlane(Graphics graphics, Bitmap surface, Rectangle view, Rectangle placement,
            NoriSceneSprite sprite, string blend)
        {
            var area = Rectangle.Intersect(view, placement);
            if (area.Width <= 0 || area.Height <= 0) return;
            using (var plane = new Bitmap(area.Width, area.Height, PixelFormat.Format32bppArgb))
            {
                placement.Offset(-area.X, -area.Y);
                using (var g = Graphics.FromImage(plane))
                {
                    g.InterpolationMode = InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = PixelOffsetMode.Half;
                    DrawPlane(g, placement, sprite);
                }
                graphics.Flush(FlushIntention.Sync);
                EffectBlend.Apply(surface, plane, area, blend);
            }
        }

        private void DrawPlane(Graphics graphics, Rectangle destination, NoriSceneSprite sprite)
        {
            float x1 = (sprite.Flip & 1) == 0 ? destination.Left : destination.Right;
            float x2 = (sprite.Flip & 1) == 0 ? destination.Right : destination.Left;
            float y1 = (sprite.Flip & 2) == 0 ? destination.Top : destination.Bottom;
            float y2 = (sprite.Flip & 2) == 0 ? destination.Bottom : destination.Top;
            var image = sprite.Image;
            var corners = new[] { new PointF(x1, y1), new PointF(x2, y1), new PointF(x1, y2) };
            var source = new RectangleF(0, 0, image.Width, image.Height);
            if (sprite.Opacity == 100)
                graphics.DrawImage(image, corners, source, GraphicsUnit.Pixel);
            else
                graphics.DrawImage(image, corners, source, GraphicsUnit.Pixel, Opacity(sprite.Opacity));
        }
    }
}
