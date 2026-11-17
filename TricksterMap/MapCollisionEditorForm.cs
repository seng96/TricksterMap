using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using TricksterMap.Data;

namespace TricksterMap
{
    public sealed class MapCollisionEditorForm : Form
    {
        public ConfigLayerEditService Edits { get; }
        private readonly CollisionCanvas canvas;
        private readonly ToolStripButton undo = new ToolStripButton("復原"), redo = new ToolStripButton("重做");
        private readonly ToolStripLabel state = new ToolStripLabel(), zoomValue = new ToolStripLabel();
        private readonly ToolStripStatusLabel position = new ToolStripStatusLabel();
        private readonly ToolStripStatusLabel sceneStatus = new ToolStripStatusLabel();
        private readonly ToolStripComboBox mode = Combo(84, "簡易預覽", "精細預覽");
        private readonly ToolStripButton overlay = new ToolStripButton("碰撞疊圖") { CheckOnClick = true, Checked = true };
        private readonly NumericUpDown brushSize = new NumericUpDown { Minimum = 1, Maximum = 128, Value = 1, Width = 52 };
        private bool verificationMode;
        private string sceneMessage = "精細場景尚未載入";
        protected override bool ShowWithoutActivation => verificationMode;
        public event EventHandler Edited;
        internal CollisionCanvas Canvas => canvas;
        public bool DetailedPreview
        {
            get => mode.SelectedIndex == 1;
            set => mode.SelectedIndex = value && canvas.ValidGridStep ? 1 : 0;
        }
        public int BrushDiameter
        {
            get => (int)brushSize.Value;
            set => brushSize.Value = Math.Max(1, Math.Min(128, value));
        }

        public MapCollisionEditorForm(ConfigLayer layer)
        {
            Text = "TricksterMap 地圖編輯器";
            Edits = new ConfigLayerEditService(layer); canvas = new CollisionCanvas(Edits);
            var first = BuildBrushToolbar(); var second = BuildViewToolbar();
            var status = new StatusStrip();
            position.Text = "滾輪縮放；中鍵／右鍵或空白鍵＋左鍵拖曳平移";
            position.Spring = true; position.TextAlign = ContentAlignment.MiddleLeft;
            sceneStatus.IsLink = true; sceneStatus.Click += (s, e) =>
                MessageBox.Show(this, sceneStatus.ToolTipText, "場景預覽狀態", MessageBoxButtons.OK, MessageBoxIcon.Information);
            status.Items.Add(position); status.Items.Add(sceneStatus);
            Controls.Add(canvas); Controls.Add(status); Controls.Add(second); Controls.Add(first);
            first.Dock = DockStyle.Top; second.Dock = DockStyle.Top;
            canvas.PositionChanged += value => position.Text = value;
            canvas.Changed += (s, e) => RefreshState();
            canvas.ViewChanged += (s, e) => zoomValue.Text = (canvas.View.Zoom * 100).ToString("0.#") + "%";
            KeyPreview = true; KeyDown += HandleKeys;
            KeyUp += (s, e) => { if (e.KeyCode == Keys.Space) { canvas.SpacePanning = false; canvas.EndInteraction(); e.Handled = true; } };
            Deactivate += (s, e) => { canvas.SpacePanning = false; canvas.EndInteraction(); };
            mode.SelectedIndex = layer.BppX > 0 && layer.BppY > 0 ? 1 : 0;
            mode.Enabled = canvas.ValidGridStep;
            RefreshState(); RefreshSceneStatus();
        }

        private ToolStrip BuildBrushToolbar()
        {
            var toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden };
            var brush = Combo(118, "阻擋 Blocked", "可通行 Walkable"); brush.SelectedIndex = 0;
            brush.SelectedIndexChanged += (s, e) => { canvas.EndInteraction(); canvas.BrushValue = brush.SelectedIndex == 0 ? (byte)1 : (byte)0; };
            var shape = Combo(64, "方形", "圓形"); shape.SelectedIndex = 0;
            shape.SelectedIndexChanged += (s, e) => { canvas.EndInteraction(); canvas.CircleBrush = shape.SelectedIndex == 1; canvas.Invalidate(); };
            brushSize.ValueChanged += (s, e) => { canvas.EndInteraction(); canvas.BrushSize = (int)brushSize.Value; canvas.Invalidate(); };
            undo.Click += (s, e) => { canvas.EndInteraction(); Edits.Undo(); RefreshState(); };
            redo.Click += (s, e) => { canvas.EndInteraction(); Edits.Redo(); RefreshState(); };
            toolbar.Items.AddRange(new ToolStripItem[] { brush, new ToolStripLabel("筆刷(格)"),
                new ToolStripControlHost(brushSize), shape, new ToolStripSeparator(), undo, redo, state });
            return toolbar;
        }

        private ToolStrip BuildViewToolbar()
        {
            var toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden };
            mode.SelectedIndexChanged += (s, e) => { canvas.Detailed = mode.SelectedIndex == 1; RefreshSceneStatus(); };
            overlay.CheckedChanged += (s, e) => { canvas.ShowCollision = overlay.Checked; canvas.Invalidate(); };
            var opacity = new NumericUpDown { Minimum = 0, Maximum = 100, Value = 35, Increment = 5, Width = 48 };
            opacity.ValueChanged += (s, e) => { canvas.OverlayOpacity = (float)opacity.Value / 100; canvas.Invalidate(); };
            var grid = new ToolStripButton("格線") { CheckOnClick = true };
            grid.CheckedChanged += (s, e) => { canvas.ShowGrid = grid.Checked; canvas.Invalidate(); };
            var fit = new ToolStripButton("符合視窗"); fit.Click += (s, e) => canvas.FitMap();
            var reset = new ToolStripButton("1:1"); reset.Click += (s, e) => canvas.ResetView();
            toolbar.Items.AddRange(new ToolStripItem[] { mode, overlay, new ToolStripControlHost(opacity),
                new ToolStripLabel("%"), grid, new ToolStripSeparator(), fit, reset, zoomValue });
            return toolbar;
        }

        private static ToolStripComboBox Combo(int width, params object[] items)
        {
            var combo = new ToolStripComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = width, AutoSize = false };
            combo.Items.AddRange(items); return combo;
        }

        public void SetSceneRenderer(IMapSceneRenderer renderer)
        {
            canvas.EndInteraction(); canvas.SceneRenderer = renderer;
            if (renderer != null) sceneMessage = "場景預覽已載入";
            RefreshSceneStatus(); canvas.Invalidate();
        }

        public void SetSceneStatus(string message)
        {
            sceneMessage = string.IsNullOrWhiteSpace(message) ? "場景狀態未提供" : message;
            RefreshSceneStatus();
        }

        private void RefreshSceneStatus()
        {
            string[] warnings = canvas.SceneRenderer?.Warnings?.Distinct().ToArray() ?? new string[0];
            string status = !canvas.ValidGridStep ? "步長無效：僅簡易預覽，精細疊圖停用" : sceneMessage;
            if (warnings.Length != 0) status += "；" + warnings.Length + "項預覽限制";
            sceneStatus.Text = status.Length > 32 ? status.Substring(0, 32) + "…" : status;
            sceneStatus.ToolTipText = status + (warnings.Length == 0 ? "" : "\r\n" + string.Join("\r\n", warnings));
            overlay.Enabled = mode.SelectedIndex == 1;
        }

        private void HandleKeys(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space)
            {
                if (!canvas.SpacePanning) { canvas.EndInteraction(); canvas.SpacePanning = true; }
                e.Handled = true; e.SuppressKeyPress = true; return;
            }
            if (!e.Control) return;
            if (e.KeyCode == Keys.Z) { canvas.EndInteraction(); Edits.Undo(); }
            else if (e.KeyCode == Keys.Y) { canvas.EndInteraction(); Edits.Redo(); }
            else if (e.KeyCode == Keys.D0) canvas.FitMap();
            else return;
            e.Handled = true; e.SuppressKeyPress = true; RefreshState();
        }

        public void MarkSaved() { canvas.EndInteraction(); Edits.MarkSaved(); RefreshState(); }
        internal void VerifyBrushStroke() { canvas.VerifyBrushStroke(); }
        internal void VerifyViewportInteraction() { canvas.VerifyWheelAndPan(); }
        internal void PrepareVerificationView()
        {
            verificationMode = true; StartPosition = FormStartPosition.Manual;
            Location = new Point(-30000, -30000); ShowInTaskbar = false; Opacity = 0; Show();
            canvas.FitMap();
        }
        private void RefreshState()
        {
            undo.Enabled = Edits.CanUndo; redo.Enabled = Edits.CanRedo;
            state.Text = Edits.IsDirty ? "未儲存" : "已儲存";
            canvas.RefreshData(); Edited?.Invoke(this, EventArgs.Empty);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) canvas.SceneRenderer = null; // Shared renderer lifecycle belongs to the document owner.
            base.Dispose(disposing);
        }
    }
}
