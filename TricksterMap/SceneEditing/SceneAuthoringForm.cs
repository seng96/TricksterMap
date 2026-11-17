using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using TricksterMap.Scene;
using TricksterMap.SceneView;

namespace TricksterMap.SceneEditing
{
    /// <summary>Edits the same Core scene used by collision preview and bundle export.</summary>
    public sealed class SceneAuthoringForm : Form
    {
        private readonly SceneRenderer renderer;
        private readonly SceneTileCanvas canvas;
        private readonly SceneTilePalette palette;
        private readonly Dictionary<SceneLayer, SceneTileEditService> histories = new Dictionary<SceneLayer, SceneTileEditService>();
        private readonly ToolStripComboBox layers = new ToolStripComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
        private readonly ToolStripComboBox groups = new ToolStripComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 125 };
        private readonly NumericUpDown tileIndex = new NumericUpDown { Width = 70, Minimum = 0, Maximum = 0 };
        private readonly NumericUpDown brushSize = new NumericUpDown { Width = 48, Minimum = 1, Maximum = 32, Value = 1 };
        private readonly ToolStripButton flipX = new ToolStripButton("水平翻轉") { CheckOnClick = true };
        private readonly ToolStripButton flipY = new ToolStripButton("垂直翻轉") { CheckOnClick = true };
        private readonly ToolStripButton erase = new ToolStripButton("橡皮擦") { CheckOnClick = true };
        private readonly ToolStripButton undo = new ToolStripButton("復原 Undo"), redo = new ToolStripButton("重做 Redo");
        private readonly ToolStripLabel state = new ToolStripLabel();
        private readonly ToolStripStatusLabel position = new ToolStripStatusLabel();
        private readonly List<SceneLayer> tileLayers;
        private SceneTileEditService current;
        private bool importedDirty, verificationMode;
        protected override bool ShowWithoutActivation => verificationMode;
        public event EventHandler Edited;
        public bool IsDirty => importedDirty || histories.Values.Any(history => history.IsDirty);

        public SceneAuthoringForm(SceneRenderer renderer)
        {
            this.renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
            Text = "地磚與場景製作"; KeyPreview = true;
            canvas = new SceneTileCanvas(renderer); palette = new SceneTilePalette(renderer);
            var sidebar = new Panel { Dock = DockStyle.Left, Width = 235 };
            sidebar.Controls.Add(palette);
            var top = new ToolStrip { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden };
            var options = new ToolStrip { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden };
            var fit = new ToolStripButton("符合畫面");
            var reset = new ToolStripButton("100%");
            var import = new ToolStripButton("匯入地磚圖集…");
            var keep = new ToolStripButton("保留其他旗標") { CheckOnClick = true, Checked = true };
            var grid = new ToolStripButton("格線") { CheckOnClick = true, Checked = true };
            top.Items.AddRange(new ToolStripItem[] { new ToolStripLabel("圖層"), layers, new ToolStripLabel("群組"), groups,
                new ToolStripLabel("地磚"), new ToolStripControlHost(tileIndex), import, fit, reset });
            options.Items.AddRange(new ToolStripItem[] { new ToolStripLabel("筆刷格數"), new ToolStripControlHost(brushSize),
                flipX, flipY, erase, keep, grid, undo, redo, state });
            var bottom = new StatusStrip(); bottom.Items.Add(position);
            position.Text = "左鍵鋪地磚；右鍵／中鍵或空白＋左鍵平移；滾輪以游標為中心縮放。";
            Controls.Add(canvas); Controls.Add(sidebar); Controls.Add(options); Controls.Add(top); Controls.Add(bottom);
            tileLayers = renderer.Scene.Layers.Where(layer => layer.Kind == 2).ToList();
            foreach (var layer in tileLayers)
            {
                histories.Add(layer, new SceneTileEditService(renderer.Scene, layer));
                layers.Items.Add("L" + renderer.Scene.Layers.IndexOf(layer) + " " + layer.Name + " (" + layer.Width + "×" + layer.Height + ")");
            }
            for (int group = 0; group < renderer.Scene.TileGroups.Count; group++)
                groups.Items.Add("群組 " + group + " (" + renderer.Scene.TileGroups[group].Width * renderer.Scene.TileGroups[group].Height + ")");
            layers.SelectedIndexChanged += (s, e) => SelectLayer();
            groups.SelectedIndexChanged += (s, e) => SelectGroup();
            tileIndex.ValueChanged += (s, e) => { palette.SelectIndex((int)tileIndex.Value); UpdateBrush(); };
            brushSize.ValueChanged += (s, e) => { canvas.FinishInteraction(); canvas.BrushSize = (int)brushSize.Value; canvas.Invalidate(); };
            palette.TileSelected += index => tileIndex.Value = Math.Min(tileIndex.Maximum, index);
            flipX.CheckedChanged += (s, e) => UpdateBrush(); flipY.CheckedChanged += (s, e) => UpdateBrush();
            erase.CheckedChanged += (s, e) => UpdateBrush();
            keep.CheckedChanged += (s, e) => { canvas.FinishInteraction(); canvas.PreserveOtherFlags = keep.Checked; };
            grid.CheckedChanged += (s, e) => { canvas.ShowGrid = grid.Checked; canvas.Invalidate(); };
            undo.Click += (s, e) => { canvas.FinishInteraction(); current?.Undo(); OnEdited(); };
            redo.Click += (s, e) => { canvas.FinishInteraction(); current?.Redo(); OnEdited(); };
            fit.Click += (s, e) => canvas.FitMap(); reset.Click += (s, e) => canvas.ResetView();
            import.Click += (s, e) => ImportTiles();
            canvas.Edited += (s, e) => OnEdited();
            canvas.PositionChanged += text => position.Text = text;
            KeyDown += HandleKeyDown; KeyUp += (s, e) =>
            { if (e.KeyCode == Keys.Space) { canvas.FinishInteraction(); canvas.SpacePanning = false; e.SuppressKeyPress = true; canvas.Invalidate(); } };
            if (layers.Items.Count > 0) layers.SelectedIndex = 0;
            else { layers.Enabled = false; position.Text = "此地圖沒有地磚圖層；仍可平移及縮放預覽。"; }
            if (groups.Items.Count > 0) groups.SelectedIndex = 0;
            else { groups.Enabled = tileIndex.Enabled = false; erase.Checked = true; }
            RefreshState();
        }

        private void SelectLayer()
        {
            canvas.FinishInteraction();
            current = layers.SelectedIndex < 0 ? null : histories[tileLayers[layers.SelectedIndex]];
            canvas.SetLayer(current); RefreshState();
        }
        private void ImportTiles()
        {
            canvas.FinishInteraction();
            int added = SceneTileImageImport.Import(this, renderer);
            if (added < 0) return;
            importedDirty = true; groups.Items.Clear();
            for (int group = 0; group < renderer.Scene.TileGroups.Count; group++)
                groups.Items.Add("群組 " + group + " (" + renderer.Scene.TileGroups[group].Width * renderer.Scene.TileGroups[group].Height + ")");
            groups.Enabled = tileIndex.Enabled = true; groups.SelectedIndex = added;
            renderer.Invalidate(); OnEdited();
        }
        private void SelectGroup()
        {
            canvas.FinishInteraction(); int group = groups.SelectedIndex;
            if (group < 0) return;
            var source = renderer.Scene.TileGroups[group];
            tileIndex.Value = 0; tileIndex.Maximum = Math.Max(0, Math.Min(65534, (long)source.Width * source.Height - 1));
            palette.SetGroup(group); UpdateBrush();
        }
        private void UpdateBrush()
        {
            canvas.FinishInteraction(); canvas.Eraser = erase.Checked || groups.SelectedIndex < 0;
            uint flags = (uint)((flipX.Checked ? 1 : 0) | (flipY.Checked ? 2 : 0));
            canvas.TileWord = groups.SelectedIndex < 0 ? 0xffffU : (uint)tileIndex.Value | (uint)groups.SelectedIndex << 16 | flags << 24;
            canvas.Invalidate();
        }
        private void HandleKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space)
            {
                if (!canvas.SpacePanning) canvas.FinishInteraction();
                canvas.SpacePanning = true; e.SuppressKeyPress = true; canvas.Invalidate();
            }
            if (!e.Control) return;
            if (e.KeyCode == Keys.Z) { canvas.FinishInteraction(); current?.Undo(); e.Handled = true; OnEdited(); }
            else if (e.KeyCode == Keys.Y) { canvas.FinishInteraction(); current?.Redo(); e.Handled = true; OnEdited(); }
        }
        private void OnEdited() { RefreshState(); canvas.Invalidate(); Edited?.Invoke(this, EventArgs.Empty); }
        private void RefreshState()
        { undo.Enabled = current != null && current.CanUndo; redo.Enabled = current != null && current.CanRedo; state.Text = IsDirty ? "尚未儲存" : "已儲存"; }
        public void MarkSaved()
        { canvas.FinishInteraction(); foreach (var history in histories.Values) history.MarkSaved(); importedDirty = false; RefreshState(); }
        public void FinishInteraction() { canvas.FinishInteraction(); }
        public void RefreshView() { canvas.Invalidate(); }

        internal void PrepareVerificationView()
        {
            verificationMode = true; StartPosition = FormStartPosition.Manual; Location = new Point(-30000, -30000);
            Opacity = 0; ShowInTaskbar = false; Show();
        }

        internal void VerifyTileStroke()
        {
            if (current == null) throw new InvalidOperationException("A tile layer is required for verification.");
            byte[] original = (byte[])current.Layer.Payload.Clone();
            erase.Checked = true; canvas.VerifyStroke();
            // The chosen cell may already be empty. Explicitly choose an existing tile when possible.
            if (!current.IsDirty && renderer.Scene.TileGroups.Count > 0)
            { erase.Checked = false; canvas.VerifyStroke(); }
            byte[] painted = (byte[])current.Layer.Payload.Clone();
            if (!current.CanUndo) throw new InvalidOperationException("Tile brush did not produce an undo step.");
            current.Undo(); if (!original.SequenceEqual(current.Layer.Payload)) throw new InvalidOperationException("Tile undo differs.");
            current.Redo(); if (!painted.SequenceEqual(current.Layer.Payload)) throw new InvalidOperationException("Tile redo differs.");
            OnEdited();
        }
    }
}
