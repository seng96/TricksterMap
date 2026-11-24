using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using TricksterMap.Data;

namespace TricksterMap
{
    internal sealed partial class ViewportMapObjectOverlay
    {
        private static readonly Color Ink = Color.FromArgb(16, 24, 39);
        private const TextFormatFlags TextFlags = TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
        private object hovered;
        private Point hoverAnchor;

        public bool SetHover(Point pointer)
        {
            object target = null;
            if (!Dragging)
            {
                if (Tool == MapObjectTool.Range && ShowRanges) target = ClosestRange(pointer);
                if (target == null && ShowPoints) target = PickPoint(pointer);
                if (target == null && ShowRanges) target = ClosestRange(pointer);
            }
            if (target == hovered) return false;
            hovered = target; hoverAnchor = pointer;
            return true;
        }

        public void ClearHover() => hovered = null;

        internal string[] HoverDetails
        {
            get
            {
                if (!Visible(hovered) || !history.Contains(hovered)) return Array.Empty<string>();
                var p = Current(hovered);
                if (hovered is PointObject point)
                    return new[]
                    {
                        $"點位 #{map.PointObjects.IndexOf(point) + 1} · {point.GetTypeName()}",
                        $"ID {point.Id}    類型 0x{point.Type:X2}",
                        $"X {p.X1}    Y {p.Y1}",
                        $"地圖 ID {point.MapId}    選項 0x{point.Options:X8}",
                    };
                var range = (RangeObject)hovered;
                return new[]
                {
                    $"範圍 #{map.RangeObjects.IndexOf(range) + 1} · {range.GetTypeName()}",
                    $"ID {range.Id}    類型 0x{range.Type:X2}",
                    $"X1 {p.X1}    Y1 {p.Y1}",
                    $"X2 {p.X2}    Y2 {p.Y2}",
                    $"跨度 {Math.Abs((long)p.X2 - p.X1)} × {Math.Abs((long)p.Y2 - p.Y1)}",
                    $"目的地 ID {range.Destination}    選項 0x{range.Options:X8}",
                };
            }
        }

        public void Draw(Graphics graphics, Rectangle clip)
        {
            if (!ShowPoints && !ShowRanges) return;
            GraphicsState state = graphics.Save();
            try
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var glyph = new Font(SystemFonts.MessageBoxFont, FontStyle.Bold))
                using (var halo = new Pen(Ink, 4.5f))
                {
                    DrawRanges(graphics, clip, halo);
                    DrawPoints(graphics, clip, glyph, halo);
                    DrawHoverCard(graphics, clip, glyph);
                }
            }
            finally { graphics.Restore(state); }
        }

        private void DrawRanges(Graphics graphics, Rectangle clip, Pen halo)
        {
            if (!ShowRanges) return;
            foreach (var group in map.RangeObjects.GroupBy(range => ViewportObjectMarkerStyle.ForRange(range.Type)))
            {
                var style = group.Key;
                using (var edge = new Pen(style.Color, 2.4f))
                using (var wash = new SolidBrush(Color.FromArgb(53, style.Color)))
                    foreach (var range in group)
                        PaintRange(graphics, Bounds(Current(range)), clip, edge, halo, wash);
            }
            if (hovered is RangeObject hoveredRange && hoveredRange != selected && history.Contains(hoveredRange))
                DrawActiveRange(graphics, clip, halo, hoveredRange, false);
            if (selected is RangeObject selectedRange && history.Contains(selectedRange))
                DrawActiveRange(graphics, clip, halo, selectedRange, true);
        }

        private void DrawActiveRange(Graphics g, Rectangle clip, Pen halo, RangeObject range, bool handles)
        {
            var p = Current(range);
            var style = ViewportObjectMarkerStyle.ForRange(range.Type);
            using (var edge = new Pen(handles ? Color.White : style.Color, 3))
            using (var wash = new SolidBrush(Color.FromArgb(60, style.Color)))
                PaintRange(g, Bounds(p), clip, edge, halo, wash);
            if (!handles) return;
            foreach (var corner in Corners)
            {
                var q = Corner(p, corner);
                if (!clip.Contains((int)q.X, (int)q.Y)) continue;
                g.FillRectangle(Brushes.White, q.X-4, q.Y-4, 8, 8);
                g.DrawRectangle(Pens.DarkSlateGray, q.X-4, q.Y-4, 8, 8);
            }
        }

        private static void PaintRange(Graphics g, RectangleF box, Rectangle clip, Pen edge, Pen halo, Brush wash)
        {
            if (box.Left > clip.Right || box.Right < clip.Left || box.Top > clip.Bottom || box.Bottom < clip.Top) return;
            var part = RectangleF.Intersect(box, clip);
            if (part.Width > 0 && part.Height > 0) g.FillRectangle(wash, part);
            DrawRangeEdges(g, box, part, halo);
            DrawRangeEdges(g, box, part, edge);
        }

        private static void DrawRangeEdges(Graphics graphics, RectangleF box, RectangleF view, Pen pen)
        {
            if (box.Top == view.Top) graphics.DrawLine(pen,view.Left,box.Top,view.Right,box.Top);
            if (box.Bottom == view.Bottom) graphics.DrawLine(pen,view.Left,box.Bottom,view.Right,box.Bottom);
            if (box.Left == view.Left) graphics.DrawLine(pen,box.Left,view.Top,box.Left,view.Bottom);
            if (box.Right == view.Right) graphics.DrawLine(pen,box.Right,view.Top,box.Right,view.Bottom);
        }

        private void DrawPoints(Graphics graphics, Rectangle clip, Font glyph, Pen halo)
        {
            if (!ShowPoints) return;
            using (var edge = new Pen(Color.FromArgb(225, Color.White), 1.1f))
                foreach (var item in map.PointObjects)
                    PaintPoint(graphics, Current(item), clip, halo, edge, glyph, ViewportObjectMarkerStyle.ForPoint(item.Type), 10);
            if (hovered is PointObject hover && hover != selected && history.Contains(hover))
                DrawActivePoint(graphics, clip, glyph, halo, hover);
            if (selected is PointObject active && history.Contains(active))
                DrawActivePoint(graphics, clip, glyph, halo, active);
        }

        private void DrawActivePoint(Graphics g, Rectangle clip, Font glyph, Pen halo, PointObject point)
        {
            var style = ViewportObjectMarkerStyle.ForPoint(point.Type);
            using (var edge = new Pen(Color.White, 2.4f))
                PaintPoint(g, Current(point), clip, halo, edge, glyph, style, 12);
        }

        private void PaintPoint(Graphics g, MapObjectPosition position, Rectangle clip,
            Pen halo, Pen outline, Font glyph, ViewportObjectMarkerStyle style, int radius)
        {
            var point = Screen(position.X1, position.Y1);
            var box = new RectangleF(point.X-radius, point.Y-radius, 2*radius, 2*radius);
            if (!box.IntersectsWith(clip)) return;
            g.DrawEllipse(halo, box);
            using (var fill = new SolidBrush(style.Color)) g.FillEllipse(fill, box);
            g.DrawEllipse(outline, box);
            TextRenderer.DrawText(g, style.Glyph, glyph, Rectangle.Round(box), Ink,
                TextFlags | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private void DrawHoverCard(Graphics g, Rectangle clip, Font titleFont)
        {
            var lines = HoverDetails;
            if (lines.Length == 0) return;
            Font normal = SystemFonts.MessageBoxFont;
            int line = Math.Max(titleFont.Height, normal.Height) + 4;
            int width = Math.Min(Math.Max(24, clip.Width-8),
                lines.Select((text, i) => TextRenderer.MeasureText(g, text, i==0 ? titleFont : normal, Size.Empty, TextFlags).Width).Max()+22);
            int height = 14 + lines.Length*line;
            int x = Math.Max(clip.Left+4, Math.Min(hoverAnchor.X+18, clip.Right-width-4));
            int down = hoverAnchor.Y+20;
            int y = Math.Max(clip.Top+4, down+height <= clip.Bottom-4 ? down : hoverAnchor.Y-height-12);
            var style = ViewportObjectMarkerStyle.For(hovered);
            using (var card = new SolidBrush(Ink))
            using (var edge = new Pen(style.Color, 1.4f))
            {
                g.FillRectangle(card, x, y, width, height);
                g.DrawRectangle(edge, x, y, width, height);
            }
            for (int i = 0; i < lines.Length; i++)
                TextRenderer.DrawText(g, lines[i], i==0 ? titleFont : normal,
                    new Rectangle(x+10,y+7+i*line,width-20,line), i==0 ? style.Color : Color.White,
                    TextFlags | TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
        }
    }
}
