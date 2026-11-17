using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using TricksterMap.Scene;
using TricksterMap.SceneView;

namespace TricksterMap.SceneEditing
{
    /// <summary>Moves existing visual objects while preserving their opaque record bytes.</summary>
    public sealed class SceneObjectEditorForm : Form
    {
        private readonly SceneRenderer renderer;
        private readonly SceneTileCanvas canvas;
        private readonly ComboBox layers = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly ListBox objects = new ListBox { Dock = DockStyle.Fill, HorizontalScrollbar = true };
        private readonly ComboBox assets = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly NumericUpDown x = Number(), y = Number(), z = Number();
        private readonly Label details = new Label { Dock = DockStyle.Bottom, Height = 42, AutoEllipsis = true };
        private readonly ToolStripLabel state = new ToolStripLabel();
        private readonly ToolStripButton undo = new ToolStripButton("復原"), redo = new ToolStripButton("重做");
        private readonly Dictionary<SceneLayer, SceneObjectEditService> histories = new Dictionary<SceneLayer, SceneObjectEditService>();
        private readonly List<SceneLayer> objectLayers;
        private SceneLayer layer;
        private SceneObjectEditService current;
        private bool verificationMode, refreshingLayerLabel;
        protected override bool ShowWithoutActivation => verificationMode;
        public event EventHandler Edited;
        public bool IsDirty => histories.Values.Any(history => history.IsDirty);

        public SceneObjectEditorForm(SceneRenderer renderer)
        {
            this.renderer = renderer ?? throw new ArgumentNullException(nameof(renderer)); Text = "場景物件位置";
            canvas = new SceneTileCanvas(renderer);
            var side = new Panel { Dock = DockStyle.Left, Width = 300 };
            var coordinates = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 88, Padding = new Padding(4), ColumnCount = 4, RowCount = 3 };
            coordinates.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20));
            coordinates.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            coordinates.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20));
            coordinates.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            x.Dock = y.Dock = z.Dock = DockStyle.Fill;
            coordinates.Controls.Add(new Label { Text = "X", Dock = DockStyle.Fill }, 0, 0); coordinates.Controls.Add(x, 1, 0);
            coordinates.Controls.Add(new Label { Text = "Y", Dock = DockStyle.Fill }, 2, 0); coordinates.Controls.Add(y, 3, 0);
            coordinates.Controls.Add(new Label { Text = "Z", Dock = DockStyle.Fill }, 0, 1); coordinates.Controls.Add(z, 1, 1);
            var apply = new Button { Text = "套用位置", Width = 90 };
            var center = new Button { Text = "定位物件", Width = 90 };
            coordinates.Controls.Add(apply, 0, 2); coordinates.SetColumnSpan(apply, 2);
            coordinates.Controls.Add(center, 2, 2); coordinates.SetColumnSpan(center, 2);
            side.Controls.Add(objects); side.Controls.Add(details); side.Controls.Add(coordinates); side.Controls.Add(layers);
            side.Controls.Add(assets); side.Controls.Add(new Label { Text = "BAC 素材：選取後雙擊地圖放置", Dock = DockStyle.Top, Height = 20 });
            var toolbar = new ToolStrip { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden };
            var fit = new ToolStripButton("符合畫面");
            var duplicate = new ToolStripButton("複製物件");
            var remove = new ToolStripButton("刪除物件");
            var add = new ToolStripButton("新增 BAC 物件");
            toolbar.Items.AddRange(new ToolStripItem[] { add, duplicate, remove, undo, redo, fit, state });
            var bottom = new StatusStrip(); var hint = new ToolStripStatusLabel("選取既有 BAC 場景物件調整 X/Y/Z；旗標、名稱與未知欄位保持原樣。");
            bottom.Items.Add(hint);
            Controls.Add(canvas); Controls.Add(side); Controls.Add(toolbar); Controls.Add(bottom);
            objectLayers = renderer.Scene.Layers.ToList();
            foreach (var item in objectLayers)
            {
                histories.Add(item, new SceneObjectEditService(item));
                layers.Items.Add(LayerLabel(item));
            }
            layers.SelectedIndexChanged += (s, e) => { if (!refreshingLayerLabel) SelectLayer(); };
            objects.SelectedIndexChanged += (s, e) => SelectObject();
            apply.Click += (s, e) => ApplyPosition(); center.Click += (s, e) => CenterObject();
            undo.Click += (s, e) => { current?.Undo(); OnEdited(); };
            redo.Click += (s, e) => { current?.Redo(); OnEdited(); };
            duplicate.Click += (s, e) => DuplicateSelected(); remove.Click += (s, e) => DeleteSelected();
            add.Click += (s, e) => AddAssetAt(renderer.Scene.Width / 2.0, renderer.Scene.Height / 2.0, true);
            canvas.PlacementRequested += (worldX, worldY) => AddAssetAt(worldX, worldY, false);
            fit.Click += (s, e) => canvas.FitMap();
            canvas.PositionChanged += text => hint.Text = text;
            KeyPreview = true;
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Space)
                { if (!canvas.SpacePanning) canvas.FinishInteraction(); canvas.SpacePanning = true; e.SuppressKeyPress = true; }
                else if (e.Control && e.KeyCode == Keys.Z) { current?.Undo(); OnEdited(); e.SuppressKeyPress = true; }
                else if (e.Control && e.KeyCode == Keys.Y) { current?.Redo(); OnEdited(); e.SuppressKeyPress = true; }
            };
            KeyUp += (s, e) => { if (e.KeyCode == Keys.Space) { canvas.FinishInteraction(); canvas.SpacePanning = false; e.SuppressKeyPress = true; } };
            foreach (var asset in renderer.AvailableObjectNames) assets.Items.Add(asset);
            if (assets.Items.Count > 0) assets.SelectedIndex = 0;
            else { assets.Enabled = add.Enabled = false; }
            if (layers.Items.Count > 0)
            {
                int main = renderer.Scene.Document.Map.Param14;
                if (main < 0 || main >= layers.Items.Count) main = objectLayers.FindIndex(item => item.Order == 6);
                layers.SelectedIndex = Math.Max(0, main);
            }
            else { layers.Enabled = objects.Enabled = apply.Enabled = center.Enabled = false; details.Text = "此場景沒有物件紀錄。"; }
            RefreshState();
        }

        private static NumericUpDown Number() => new NumericUpDown { Width = 70, Minimum = int.MinValue, Maximum = int.MaxValue };
        private string LayerLabel(SceneLayer item) => "L" + renderer.Scene.Layers.IndexOf(item) + " " + item.Name + " (" + item.Objects.Count + ")";
        private void SelectLayer()
        {
            layer = layers.SelectedIndex < 0 ? null : objectLayers[layers.SelectedIndex];
            current = layer == null ? null : histories[layer];
            RefreshList(0); RefreshState();
        }
        private void RefreshList(int selected)
        {
            objects.BeginUpdate(); objects.Items.Clear();
            if (layer != null)
                for (int i = 0; i < layer.Objects.Count; i++) objects.Items.Add(i + ": " + layer.Objects[i].Name);
            if (objects.Items.Count > 0) objects.SelectedIndex = Math.Max(0, Math.Min(selected, objects.Items.Count - 1));
            objects.EndUpdate(); SelectObject();
        }
        private void SelectObject()
        {
            if (layer == null || objects.SelectedIndex < 0) { canvas.SelectionMarkerWorld = null; canvas.Invalidate(); return; }
            var item = layer.Objects[objects.SelectedIndex]; x.Value = item.X; y.Value = item.Y; z.Value = item.Z;
            details.Text = "原始 flags 0x" + item.Flags.ToString("X8") + "（保留）\r\n原始紀錄 " + item.RawData.Length + " bytes";
            canvas.SelectionMarkerWorld = new PointF((float)((double)layer.X + item.X), (float)((double)layer.Y + item.Y - item.Z)); canvas.Invalidate();
        }
        private void ApplyPosition()
        {
            if (current == null || objects.SelectedIndex < 0) return;
            current.SetPosition(objects.SelectedIndex, (int)x.Value, (int)y.Value, (int)z.Value); OnEdited();
        }
        private void DuplicateSelected()
        {
            if (current == null || objects.SelectedIndex < 0) return;
            int added = current.Duplicate(objects.SelectedIndex); RefreshList(added); OnEdited();
        }
        private void AddAssetAt(double worldX, double worldY, bool center)
        {
            var asset = assets.SelectedItem as SceneObjectAsset;
            if (current == null || layer == null || asset == null)
            { details.Text = "請先選擇圖層和有效 BAC 素材。"; return; }
            if (worldX < 0 || worldY < 0 || worldX >= renderer.Scene.Width || worldY >= renderer.Scene.Height)
            { details.Text = "請在地圖範圍內放置物件。"; return; }
            try
            {
                var item = new SceneObject(new byte[renderer.Scene.Version == 301 ? 48 : 56]);
                item.NameBytes = asset.NameBytes; item.X = checked((int)Math.Round(worldX - layer.X));
                item.Y = checked((int)Math.Round(worldY - layer.Y));
                int added = current.Add(item); RefreshList(added); OnEdited();
                if (center) CenterObject();
            }
            catch (Exception ex) when (ex is ArgumentException || ex is OverflowException)
            { details.Text = "物件未加入：" + ex.Message; }
        }
        private void DeleteSelected()
        {
            if (current == null || objects.SelectedIndex < 0) return;
            int selected = objects.SelectedIndex; current.Delete(selected); RefreshList(selected); OnEdited();
        }
        private void CenterObject()
        {
            if (!canvas.SelectionMarkerWorld.HasValue) return;
            PointF point = canvas.SelectionMarkerWorld.Value;
            canvas.View.Pan(canvas.Width / 2.0 - canvas.View.ScreenX(point.X), canvas.Height / 2.0 - canvas.View.ScreenY(point.Y));
            canvas.Invalidate();
        }
        private void OnEdited()
        {
            if (layers.SelectedIndex >= 0)
            {
                refreshingLayerLabel = true;
                try { layers.Items[layers.SelectedIndex] = LayerLabel(layer); }
                finally { refreshingLayerLabel = false; }
            }
            RefreshList(objects.SelectedIndex); RefreshState(); canvas.Invalidate(); Edited?.Invoke(this, EventArgs.Empty);
        }
        private void RefreshState()
        { undo.Enabled = current != null && current.CanUndo; redo.Enabled = current != null && current.CanRedo; state.Text = IsDirty ? "尚未儲存" : "已儲存"; }
        public void MarkSaved() { canvas.FinishInteraction(); foreach (var history in histories.Values) history.MarkSaved(); RefreshState(); }
        public void RefreshView() { canvas.Invalidate(); }
        public void FinishInteraction() { canvas.FinishInteraction(); }
        internal void PrepareVerificationView()
        {
            verificationMode = true; StartPosition = FormStartPosition.Manual; Location = new Point(-30000, -30000);
            Opacity = 0; ShowInTaskbar = false; Show();
        }
        internal void VerifyObjectEdit()
        {
            int populated = objectLayers.FindIndex(item => item.Objects.Count > 0);
            if (populated >= 0) layers.SelectedIndex = populated;
            if (layer == null || layer.Objects.Count == 0) throw new InvalidOperationException("Object verification requires an existing object.");
            objects.SelectedIndex = 0; var original = (byte[])layer.Objects[0].RawData.Clone();
            int beforeCount = layer.Objects.Count;
            x.Value = layer.Objects[0].X == int.MaxValue ? layer.Objects[0].X - 1 : layer.Objects[0].X + 1;
            ApplyPosition();
            if (!current.CanUndo || original.SequenceEqual(layer.Objects[0].RawData)) throw new InvalidOperationException("Object position edit did not apply.");
            byte[] moved = (byte[])layer.Objects[0].RawData.Clone();
            current.Undo(); if (!original.SequenceEqual(layer.Objects[0].RawData)) throw new InvalidOperationException("Object position undo differs.");
            current.Redo(); if (!moved.SequenceEqual(layer.Objects[0].RawData)) throw new InvalidOperationException("Object position redo differs.");
            DuplicateSelected(); if (layer.Objects.Count != beforeCount + 1 || !moved.SequenceEqual(layer.Objects[1].RawData))
                throw new InvalidOperationException("Object duplicate did not preserve the record.");
            DeleteSelected(); if (layer.Objects.Count != beforeCount) throw new InvalidOperationException("Object delete did not apply.");
            current.Undo(); if (layer.Objects.Count != beforeCount + 1) throw new InvalidOperationException("Object delete undo differs.");
            OnEdited();
        }
        internal void VerifyAddObject()
        {
            if (assets.Items.Count == 0 || layer == null) throw new InvalidOperationException("BAC asset is required for placement verification.");
            int beforeCount = layer.Objects.Count;
            var expectedName = ((SceneObjectAsset)assets.SelectedItem).NameBytes;
            AddAssetAt(renderer.Scene.Width / 2.0, renderer.Scene.Height / 2.0, false);
            if (layer.Objects.Count != beforeCount + 1 || !expectedName.SequenceEqual(layer.Objects.Last().NameBytes))
                throw new InvalidOperationException("BAC object placement did not retain the original name bytes.");
            byte[] added = (byte[])layer.Objects.Last().RawData.Clone();
            if (layer.Objects.Last().Flags != 0 || layer.Objects.Last().Z != 0 ||
                added.Length != (renderer.Scene.Version == 301 ? 48 : 56) || added.Skip(48).Any(value => value != 0))
                throw new InvalidOperationException("New BAC placement defaults differ from the documented zero record.");
            current.Undo(); if (layer.Objects.Count != beforeCount) throw new InvalidOperationException("BAC placement undo differs.");
            current.Redo(); if (layer.Objects.Count != beforeCount + 1 || !added.SequenceEqual(layer.Objects.Last().RawData))
                throw new InvalidOperationException("BAC placement redo differs.");
            OnEdited();
        }
    }
}
