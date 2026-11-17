using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TricksterMap
{
    /// <summary>Fixed-size viewport: wheel zoom, pan and brush share one map transform.</summary>
    internal sealed class CollisionCanvas : Control
    {
        private readonly ConfigLayerEditService edits;
        private readonly CollisionGridImage grid;
        private readonly ViewportBrushFootprint footprint = new ViewportBrushFootprint();
        private Point? previous, hover;
        private Point panStart;
        private bool stroking, panning, verifying, detailed, autoFit = true;
        public readonly ViewportTransform View = new ViewportTransform();
        public byte BrushValue { get; set; } = 1;
        public int BrushSize { get; set; } = 1;
        public bool CircleBrush { get; set; }
        public bool SpacePanning { get; set; }
        public bool ShowCollision { get; set; } = true;
        public float OverlayOpacity { get; set; } = .35f;
        public bool ShowGrid { get; set; }
        public IMapSceneRenderer SceneRenderer { get; set; }
        public event EventHandler Changed, ViewChanged;
        public event Action<string> PositionChanged;
        public bool ValidGridStep => edits.Layer.BppX > 0 && edits.Layer.BppY > 0;
        private int StepX => ValidGridStep ? edits.Layer.BppX : 16;
        private int StepY => ValidGridStep ? edits.Layer.BppY : 16;
        public bool Detailed
        {
            get => detailed;
            set { Finish(); detailed = value && ValidGridStep; Invalidate(); }
        }

        public CollisionCanvas(ConfigLayerEditService edits)
        {
            this.edits = edits; grid = new CollisionGridImage(edits.Layer);
            Dock = DockStyle.Fill; BackColor = Color.FromArgb(30, 35, 40); DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.Selectable, true); TabStop = true;
            MouseDown += Start; MouseMove += MovePointer; MouseWheel += Wheel;
            MouseEnter += (s, e) => { if (Form.ActiveForm != null && Form.ActiveForm.Contains(this)) Focus(); };
            MouseUp += (s, e) => Finish(); MouseLeave += (s, e) => { hover = null; Invalidate(); };
            MouseCaptureChanged += (s, e) => { if (!Capture) Finish(); };
            LostFocus += (s, e) => { SpacePanning = false; Finish(); };
        }

        public void FitMap()
        {
            Finish(); autoFit = true;
            View.Fit(edits.Layer.X * (double)StepX, edits.Layer.Y * (double)StepY, ClientSize.Width, ClientSize.Height);
            ViewChanged?.Invoke(this, EventArgs.Empty); Invalidate();
        }
        public void ResetView()
        {
            Finish(); autoFit = false;
            View.Reset(edits.Layer.X * (double)StepX, edits.Layer.Y * (double)StepY, ClientSize.Width, ClientSize.Height);
            ViewChanged?.Invoke(this, EventArgs.Empty); Invalidate();
        }
        public void SetZoom(double zoom)
        {
            Finish(); autoFit = false; View.ZoomAt(ClientSize.Width / 2.0, ClientSize.Height / 2.0, zoom);
            ViewChanged?.Invoke(this, EventArgs.Empty); Invalidate();
        }
        public void RefreshData() { grid.Invalidate(); Invalidate(); }
        public void EndInteraction() { Finish(); }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (autoFit && ClientSize.Width > 50 && ClientSize.Height > 50) FitMap();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Detailed && SceneRenderer != null)
            {
                try { SceneRenderer.Render(e.Graphics, ClientRectangle, View.Zoom, View.OriginX, View.OriginY); }
                catch (Exception ex) { PositionChanged?.Invoke("場景預覽失敗，碰撞資料未變更：" + ex.Message); }
            }
            var graphics = e.Graphics;
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor; graphics.PixelOffsetMode = PixelOffsetMode.Half;
            var bounds = new RectangleF((float)View.OriginX, (float)View.OriginY,
                (float)(edits.Layer.X * (double)StepX * View.Zoom), (float)(edits.Layer.Y * (double)StepY * View.Zoom));
            if (!Detailed || ShowCollision || SceneRenderer == null)
                grid.Draw(graphics, bounds, Detailed && SceneRenderer != null ? OverlayOpacity : 1);
            if (ShowGrid && StepX * View.Zoom >= 8 && StepY * View.Zoom >= 8) DrawGrid(graphics);
            if (hover.HasValue && !panning && !SpacePanning)
            {
                int size = Math.Max(1, Math.Min(128, BrushSize));
                var box = new RectangleF((float)View.ScreenX((hover.Value.X - size / 2) * (double)StepX),
                    (float)View.ScreenY((hover.Value.Y - size / 2) * (double)StepY),
                    (float)(size * StepX * View.Zoom), (float)(size * StepY * View.Zoom));
                footprint.Draw(graphics, box, bounds, size, CircleBrush);
                using (var pen = new Pen(Color.Yellow, 2))
                    if (CircleBrush) graphics.DrawEllipse(pen, box); else graphics.DrawRectangle(pen, box.X, box.Y, box.Width, box.Height);
            }
        }

        private void DrawGrid(Graphics graphics)
        {
            int left = Math.Max(0, (int)Math.Floor(View.WorldX(0) / StepX));
            int top = Math.Max(0, (int)Math.Floor(View.WorldY(0) / StepY));
            int right = Math.Min(edits.Layer.X, (int)Math.Ceiling(View.WorldX(Width) / StepX));
            int bottom = Math.Min(edits.Layer.Y, (int)Math.Ceiling(View.WorldY(Height) / StepY));
            using (var pen = new Pen(Color.FromArgb(70, Color.White)))
            {
                for (int x = left; x <= right; x++) graphics.DrawLine(pen, (float)View.ScreenX(x * (double)StepX),
                    (float)View.ScreenY(top * (double)StepY), (float)View.ScreenX(x * (double)StepX), (float)View.ScreenY(bottom * (double)StepY));
                for (int y = top; y <= bottom; y++) graphics.DrawLine(pen, (float)View.ScreenX(left * (double)StepX),
                    (float)View.ScreenY(y * (double)StepY), (float)View.ScreenX(right * (double)StepX), (float)View.ScreenY(y * (double)StepY));
            }
        }
        private Point? Cell(Point point)
        {
            int x = (int)Math.Floor(View.WorldX(point.X) / StepX), y = (int)Math.Floor(View.WorldY(point.Y) / StepY);
            return x < 0 || y < 0 || x >= edits.Layer.X || y >= edits.Layer.Y ? (Point?)null : new Point(x, y);
        }

        private void Start(object sender, MouseEventArgs e)
        {
            if (!verifying) Focus();
            if (e.Button == MouseButtons.Middle || e.Button == MouseButtons.Right || (SpacePanning && e.Button == MouseButtons.Left))
            {
                Finish(); autoFit = false; panning = true; panStart = e.Location; Cursor = Cursors.Hand;
                if (!verifying) Capture = true;
                return;
            }
            if (e.Button != MouseButtons.Left) return;
            Point? cell = Cell(e.Location); if (!cell.HasValue) return;
            Finish(); edits.BeginStroke(); previous = cell; stroking = true;
            if (!verifying) Capture = true;
            PaintSegment(previous.Value, previous.Value);
        }
        private void MovePointer(object sender, MouseEventArgs e)
        {
            if (panning)
            {
                View.Pan(e.X - panStart.X, e.Y - panStart.Y); panStart = e.Location;
                ViewChanged?.Invoke(this, EventArgs.Empty); Invalidate(); return;
            }
            hover = Cell(e.Location);
            if (!hover.HasValue) { previous = null; Invalidate(); return; }
            Point cell = hover.Value;
            PositionChanged?.Invoke(string.Format("格 ({0},{1})  原值 {2}  世界 ({3:0.##},{4:0.##})  筆刷 {5}格",
                cell.X, cell.Y, edits.Layer.Data[cell.Y * edits.Layer.X + cell.X], View.WorldX(e.X), View.WorldY(e.Y), BrushSize));
            if (stroking && e.Button == MouseButtons.Left) { PaintSegment(previous ?? cell, cell); previous = cell; }
            Invalidate();
        }
        private void PaintSegment(Point from, Point to)
        {
            edits.DrawBrushLine(from.X, from.Y, to.X, to.Y, Math.Max(1, Math.Min(128, BrushSize)), BrushValue, CircleBrush);
            grid.Invalidate(); Invalidate();
        }
        private void Wheel(object sender, MouseEventArgs e)
        {
            Finish(); autoFit = false;
            View.ZoomAt(e.X, e.Y, View.Zoom * Math.Pow(1.2, e.Delta / 120.0));
            ViewChanged?.Invoke(this, EventArgs.Empty); Invalidate();
        }
        private void Finish()
        {
            bool active = stroking;
            edits.EndStroke(); previous = null; stroking = false; panning = false; Cursor = Cursors.Default;
            if (!verifying && Capture) Capture = false;
            if (active) Changed?.Invoke(this, EventArgs.Empty);
        }

        internal void VerifyBrushStroke()
        {
            verifying = true;
            try
            {
                BrushValue = edits.Layer.Data[0] == 0 ? (byte)1 : (byte)0;
                var a = new Point((int)View.ScreenX(StepX * .5), (int)View.ScreenY(StepY * .5));
                var b = new Point((int)View.ScreenX((Math.Min(3, edits.Layer.X - 1) + .5) * StepX),
                    (int)View.ScreenY((Math.Min(2, edits.Layer.Y - 1) + .5) * StepY));
                Start(this, new MouseEventArgs(MouseButtons.Left, 1, a.X, a.Y, 0));
                MovePointer(this, new MouseEventArgs(MouseButtons.Left, 0, b.X, b.Y, 0)); Finish();
            }
            finally { verifying = false; }
        }
        internal void VerifyWheelAndPan()
        {
            verifying = true;
            double savedZoom = View.Zoom, savedX = View.OriginX, savedY = View.OriginY;
            bool savedFit = autoFit;
            try
            {
                double x = View.WorldX(100), y = View.WorldY(80);
                Wheel(this, new MouseEventArgs(MouseButtons.None, 0, 100, 80, 120));
                if (Math.Abs(View.WorldX(100) - x) > 1e-7 || Math.Abs(View.WorldY(80) - y) > 1e-7)
                    throw new InvalidOperationException("Wheel zoom moved its world anchor.");
                foreach (MouseButtons button in new[] { MouseButtons.Middle, MouseButtons.Right, MouseButtons.Left })
                {
                    byte[] before = (byte[])edits.Layer.Data.Clone();
                    bool undo = edits.CanUndo, redo = edits.CanRedo, dirty = edits.IsDirty;
                    double originX = View.OriginX, originY = View.OriginY;
                    SpacePanning = button == MouseButtons.Left;
                    Start(this, new MouseEventArgs(button, 1, 80, 80, 0));
                    MovePointer(this, new MouseEventArgs(button, 0, 110, 100, 0)); Finish();
                    if (View.OriginX != originX + 30 || View.OriginY != originY + 20)
                        throw new InvalidOperationException("Pan did not move the viewport by the pointer delta.");
                    if (edits.CanUndo != undo || edits.CanRedo != redo || edits.IsDirty != dirty)
                        throw new InvalidOperationException("Panning changed undo/redo or saved state.");
                    for (int i = 0; i < before.Length; i++) if (before[i] != edits.Layer.Data[i])
                        throw new InvalidOperationException("Panning modified collision cells.");
                }
            }
            finally
            {
                SpacePanning = false; Finish(); verifying = false;
                View.ZoomAt(0, 0, savedZoom); View.Pan(savedX - View.OriginX, savedY - View.OriginY); autoFit = savedFit;
                ViewChanged?.Invoke(this, EventArgs.Empty); Invalidate();
            }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { grid.Dispose(); footprint.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
