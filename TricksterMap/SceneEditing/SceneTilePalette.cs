using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using TricksterMap.SceneView;

namespace TricksterMap.SceneEditing
{
    /// <summary>Virtual palette: only visible thumbnails are decoded and painted.</summary>
    internal sealed class SceneTilePalette : ScrollableControl
    {
        private readonly SceneRenderer renderer;
        private const int CellWidth = 52, CellHeight = 66;
        private int group = -1, selected;
        public event Action<int> TileSelected;
        private int Columns => Math.Max(1, (ClientSize.Width - SystemInformation.VerticalScrollBarWidth) / CellWidth);
        private int Count => group < 0 || group >= renderer.Scene.TileGroups.Count ? 0 :
            Math.Min(65535, checked(renderer.Scene.TileGroups[group].Width * renderer.Scene.TileGroups[group].Height));

        public SceneTilePalette(SceneRenderer renderer)
        {
            this.renderer = renderer; AutoScroll = true; DoubleBuffered = true;
            BackColor = Color.FromArgb(42, 48, 54); ForeColor = Color.White; Dock = DockStyle.Fill;
            Resize += (s, e) => UpdateExtent();
            MouseDown += SelectTile;
        }
        public void SetGroup(int value) { group = value; selected = 0; AutoScrollPosition = Point.Empty; UpdateExtent(); }
        public void SelectIndex(int value) { selected = value; Invalidate(); }
        private void UpdateExtent()
        { AutoScrollMinSize = new Size(0, ((Count + Columns - 1) / Columns) * CellHeight); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
            int yOffset = AutoScrollPosition.Y;
            int row0 = Math.Max(0, (e.ClipRectangle.Top - yOffset) / CellHeight);
            int row1 = Math.Min((Count + Columns - 1) / Columns, (e.ClipRectangle.Bottom - yOffset) / CellHeight + 1);
            for (int row = row0; row < row1; row++)
                for (int column = 0; column < Columns; column++)
                {
                    int index = row * Columns + column; if (index >= Count) break;
                    int x = column * CellWidth + 2, y = row * CellHeight + yOffset + 2;
                    e.Graphics.FillRectangle(Brushes.DimGray, x, y, 48, 48);
                    try
                    {
                        using (var image = renderer.GetTileThumbnailClone(group, index))
                            e.Graphics.DrawImage(image, new Rectangle(x, y, 48, 48));
                    }
                    catch (Exception ex) when (ex is ArgumentException || ex is System.IO.IOException)
                    { e.Graphics.DrawLine(Pens.Red, x, y, x + 48, y + 48); }
                    if (index == selected) using (var pen = new Pen(Color.Yellow, 2)) e.Graphics.DrawRectangle(pen, x, y, 48, 48);
                    TextRenderer.DrawText(e.Graphics, index.ToString(), Font, new Rectangle(x, y + 49, 48, 14), Color.White);
                }
        }

        private void SelectTile(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            int index = (e.Y - AutoScrollPosition.Y) / CellHeight * Columns + e.X / CellWidth;
            if (index < 0 || index >= Count) return;
            selected = index; TileSelected?.Invoke(index); Invalidate();
        }
    }
}
