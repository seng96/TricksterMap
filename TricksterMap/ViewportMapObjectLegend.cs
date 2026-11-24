using System.Drawing;
using System.Windows.Forms;

namespace TricksterMap
{
    internal static class ViewportMapObjectLegend
    {
        public static ToolStripDropDownButton Create()
        {
            var legend = new ToolStripDropDownButton("圖例");
            AddCategory(legend, "點位", ViewportObjectMarkerStyle.Points);
            AddCategory(legend, "範圍", ViewportObjectMarkerStyle.Ranges);
            return legend;
        }

        private static void AddCategory(ToolStripDropDownButton legend, string title, ViewportObjectMarkerStyle[] styles)
        {
            var group = new ToolStripMenuItem(title);
            legend.DropDownItems.Add(group);
            foreach (var style in styles)
            {
                string caption = title == "範圍" && style == ViewportObjectMarkerStyle.Npc ? "NPC 移動" : style.Caption;
                var swatch = new Bitmap(16, 16);
                using (var graphics = Graphics.FromImage(swatch))
                using (var fill = new SolidBrush(style.Color))
                {
                    graphics.FillRectangle(fill, 1, 1, 14, 14);
                    graphics.DrawRectangle(Pens.DarkSlateGray, 1, 1, 13, 13);
                }
                var item = new ToolStripLabel(title == "範圍" ? caption : $"{style.Glyph}  {caption}", swatch);
                item.Disposed += (s, e) => swatch.Dispose();
                group.DropDownItems.Add(item);
            }
        }
    }
}
