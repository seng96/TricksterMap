using System;
using System.Drawing;
using System.Windows.Forms;
using TricksterMap.Scene;
using TricksterMap.SceneView;

namespace TricksterMap.SceneEditing
{
    internal sealed class SceneTileCanvas : Control
    {
        private readonly SceneRenderer renderer;
        private SceneTileEditService edits;
        private bool stroking, panning, verifying, autoFit = true;
        private Point panStart;
        private Point? previous, hover;
        public readonly ViewportTransform View = new ViewportTransform();
        public uint TileWord { get; set; }
        public int BrushSize { get; set; } = 1;
        public bool Eraser { get; set; }
        public bool SpacePanning { get; set; }
        public bool PreserveOtherFlags { get; set; } = true;
        public bool ShowGrid { get; set; } = true;
        public PointF? SelectionMarkerWorld { get; set; }
        public event EventHandler Edited, ViewChanged;
        public event Action<string> PositionChanged;
        public event Action<double, double> PlacementRequested;

        public SceneTileCanvas(SceneRenderer renderer)
        {
            this.renderer = renderer; Dock = DockStyle.Fill; DoubleBuffered = true; TabStop = true;
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            MouseDown += Start; MouseMove += MovePointer; MouseUp += (s, e) => FinishInteraction();
            MouseWheel += Wheel; MouseLeave += (s, e) => { hover = null; Invalidate(); };
            MouseDoubleClick += (s, e) =>
            {
                if (edits == null && e.Button == MouseButtons.Left && !SpacePanning)
                    PlacementRequested?.Invoke(View.WorldX(e.X), View.WorldY(e.Y));
            };
            MouseCaptureChanged += (s, e) => { if (!Capture) FinishInteraction(); };
            LostFocus += (s, e) => { SpacePanning = false; FinishInteraction(); };
        }

        public void SetLayer(SceneTileEditService value) { FinishInteraction(); edits = value; Invalidate(); }
        public void FitMap()
        {
            FinishInteraction(); autoFit = true;
            View.Fit(renderer.Scene.Width, renderer.Scene.Height, Width, Height); Invalidate(); ViewChanged?.Invoke(this, EventArgs.Empty);
        }
        public void ResetView()
        {
            FinishInteraction(); autoFit = false;
            View.Reset(renderer.Scene.Width, renderer.Scene.Height, Width, Height); Invalidate(); ViewChanged?.Invoke(this, EventArgs.Empty);
        }
        protected override void OnResize(EventArgs e)
        { base.OnResize(e); if (autoFit && Width > 40 && Height > 40) FitMap(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); renderer.Render(e.Graphics, ClientRectangle, View.Zoom, View.OriginX, View.OriginY);
            if (SelectionMarkerWorld.HasValue)
            {
                float markerX = (float)View.ScreenX(SelectionMarkerWorld.Value.X), markerY = (float)View.ScreenY(SelectionMarkerWorld.Value.Y);
                using (var pen = new Pen(Color.Yellow, 2))
                {
                    e.Graphics.DrawEllipse(pen, markerX - 6, markerY - 6, 12, 12);
                    e.Graphics.DrawLine(pen, markerX - 13, markerY, markerX + 13, markerY);
                    e.Graphics.DrawLine(pen, markerX, markerY - 13, markerX, markerY + 13);
                }
            }
            if (edits == null) return;
            var layer = edits.Layer;
            int stepX = renderer.Scene.TileWidth, stepY = renderer.Scene.TileHeight;
            if (ShowGrid && stepX * View.Zoom >= 12 && stepY * View.Zoom >= 12)
            {
                int left = Math.Max(0, (int)Math.Floor((View.WorldX(0) - layer.X) / stepX));
                int top = Math.Max(0, (int)Math.Floor((View.WorldY(0) - layer.Y) / stepY));
                int right = Math.Min(layer.Width, (int)Math.Ceiling((View.WorldX(Width) - layer.X) / stepX));
                int bottom = Math.Min(layer.Height, (int)Math.Ceiling((View.WorldY(Height) - layer.Y) / stepY));
                using (var pen = new Pen(Color.FromArgb(65, Color.White)))
                {
                    for (int x = left; x <= right; x++) e.Graphics.DrawLine(pen, (float)View.ScreenX(layer.X + x * stepX),
                        (float)View.ScreenY(layer.Y + top * stepY), (float)View.ScreenX(layer.X + x * stepX), (float)View.ScreenY(layer.Y + bottom * stepY));
                    for (int y = top; y <= bottom; y++) e.Graphics.DrawLine(pen, (float)View.ScreenX(layer.X + left * stepX),
                        (float)View.ScreenY(layer.Y + y * stepY), (float)View.ScreenX(layer.X + right * stepX), (float)View.ScreenY(layer.Y + y * stepY));
                }
            }
            if (hover.HasValue && !panning && !SpacePanning)
            {
                int size = Math.Max(1, Math.Min(32, BrushSize));
                var box = new RectangleF((float)View.ScreenX(layer.X + (hover.Value.X - size / 2) * stepX),
                    (float)View.ScreenY(layer.Y + (hover.Value.Y - size / 2) * stepY),
                    (float)(size * stepX * View.Zoom), (float)(size * stepY * View.Zoom));
                using (var pen = new Pen(Color.Yellow, 2)) e.Graphics.DrawRectangle(pen, box.X, box.Y, box.Width, box.Height);
            }
        }

        private Point? Cell(Point screen)
        {
            if (edits == null) return null;
            int x = (int)Math.Floor((View.WorldX(screen.X) - edits.Layer.X) / renderer.Scene.TileWidth);
            int y = (int)Math.Floor((View.WorldY(screen.Y) - edits.Layer.Y) / renderer.Scene.TileHeight);
            return x < 0 || y < 0 || x >= edits.Layer.Width || y >= edits.Layer.Height ? (Point?)null : new Point(x, y);
        }
        private void Start(object sender, MouseEventArgs e)
        {
            if (!verifying) Focus();
            FinishInteraction();
            if (e.Button == MouseButtons.Right || e.Button == MouseButtons.Middle || (e.Button == MouseButtons.Left && SpacePanning))
            {
                panning = true; panStart = e.Location; autoFit = false; Cursor = Cursors.Hand;
                if (!verifying) Capture = true; return;
            }
            if (e.Button != MouseButtons.Left || edits == null) return;
            if (!Eraser && (TileWord & 65535) != 65535)
            {
                int group = (int)((TileWord >> 16) & 255), tile = (int)(TileWord & 65535);
                if (group >= renderer.Scene.TileGroups.Count || tile >= (long)renderer.Scene.TileGroups[group].Width * renderer.Scene.TileGroups[group].Height)
                { PositionChanged?.Invoke("請先選擇有效的地磚群組與編號，或使用橡皮擦。"); return; }
            }
            var cell = Cell(e.Location); if (!cell.HasValue) return;
            edits.BeginStroke(); stroking = true; previous = cell;
            if (!verifying) Capture = true;
            PaintSegment(cell.Value, cell.Value); Invalidate();
        }
        private void MovePointer(object sender, MouseEventArgs e)
        {
            if (panning)
            { View.Pan(e.X - panStart.X, e.Y - panStart.Y); panStart = e.Location; ViewChanged?.Invoke(this, EventArgs.Empty); Invalidate(); return; }
            hover = Cell(e.Location);
            if (!hover.HasValue) { previous = null; Invalidate(); return; }
            var cell = hover.Value;
            PositionChanged?.Invoke(string.Format("地磚格 ({0},{1})  原值 0x{2:X8}  世界 ({3:0.#},{4:0.#})",
                cell.X, cell.Y, edits.Layer.GetTile(cell.X, cell.Y), View.WorldX(e.X), View.WorldY(e.Y)));
            if (stroking && e.Button == MouseButtons.Left) { PaintSegment(previous ?? cell, cell); previous = cell; }
            Invalidate();
        }
        private void PaintSegment(Point from, Point to)
        {
            int dx = Math.Abs(to.X - from.X), dy = -Math.Abs(to.Y - from.Y);
            int sx = from.X < to.X ? 1 : -1, sy = from.Y < to.Y ? 1 : -1, error = dx + dy;
            while (true)
            {
                Stamp(from);
                if (from == to) break;
                int twice = error * 2;
                if (twice >= dy) { error += dy; from.X += sx; }
                if (twice <= dx) { error += dx; from.Y += sy; }
            }
        }
        private void Stamp(Point center)
        {
            int size = Math.Max(1, Math.Min(32, BrushSize)), left = center.X - size / 2, top = center.Y - size / 2;
            for (int y = Math.Max(0, top); y < Math.Min(edits.Layer.Height, top + size); y++)
                for (int x = Math.Max(0, left); x < Math.Min(edits.Layer.Width, left + size); x++)
                {
                    uint before = edits.Layer.GetTile(x, y);
                    uint word = Eraser ? before | 0xffffU : (TileWord & 0x03ffffffU) | (PreserveOtherFlags ? before & 0xfc000000U : 0);
                    edits.SetCell(x, y, word);
                }
        }
        private void Wheel(object sender, MouseEventArgs e)
        {
            FinishInteraction(); autoFit = false; View.ZoomAt(e.X, e.Y, View.Zoom * Math.Pow(1.2, e.Delta / 120.0));
            ViewChanged?.Invoke(this, EventArgs.Empty); Invalidate();
        }
        public void FinishInteraction()
        {
            bool active = stroking; edits?.EndStroke(); stroking = panning = false; previous = null; Cursor = Cursors.Default;
            if (!verifying && Capture) Capture = false;
            if (active) Edited?.Invoke(this, EventArgs.Empty);
        }
        internal void VerifyStroke()
        {
            if (edits == null) throw new InvalidOperationException("A tile layer is required.");
            verifying = true;
            try
            {
                Point cell = new Point(Math.Min(1, edits.Layer.Width - 1), Math.Min(1, edits.Layer.Height - 1));
                var screen = new Point((int)View.ScreenX(edits.Layer.X + (cell.X + .5) * renderer.Scene.TileWidth),
                    (int)View.ScreenY(edits.Layer.Y + (cell.Y + .5) * renderer.Scene.TileHeight));
                Start(this, new MouseEventArgs(MouseButtons.Left, 1, screen.X, screen.Y, 0)); FinishInteraction();
            }
            finally { verifying = false; }
        }
    }
}
