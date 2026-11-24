using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using TricksterMap.SceneView;
using TricksterMap.Scene;
using TricksterMap.SceneEditing;

namespace TricksterMap
{
    public partial class MDIParent : Form
    {
        private static readonly string Caption = "TricksterMap " + typeof(MDIParent).Assembly.GetName().Version.ToString(3);
        private MapDocument document;
        private string savedFingerprint;
        private bool changedLanguage;
        private List<MapCollisionEditorForm> collisionEditors = new List<MapCollisionEditorForm>();
        private readonly ScenePreviewLoader sceneLoader;
        private string savedSceneFingerprint;
        private SceneAuthoringForm sceneEditor;
        private SceneObjectEditorForm objectEditor;
        private readonly SceneAnimationController animation;
        private bool verificationMode;
        protected override bool ShowWithoutActivation => verificationMode;
        internal MapDocument EditingDocument => document;
        internal SceneRenderer Preview => sceneLoader.Current;
        internal IList<MapCollisionEditorForm> CollisionEditors => collisionEditors;

        internal void PrepareVerificationView()
        {
            verificationMode = true; StartPosition = FormStartPosition.Manual;
            Location = new System.Drawing.Point(-30000, -30000); ShowInTaskbar = false;
            Opacity = 0; Size = new System.Drawing.Size(1100, 800); Show();
        }

        public MDIParent()
        {
            InitializeComponent();
            Text = Caption;
            animation = new SceneAnimationController(() =>
            {
                foreach (var editor in collisionEditors)
                    if (!editor.IsDisposed && !editor.Disposing && editor.Visible && editor.DetailedPreview) editor.Canvas.Invalidate();
                if (sceneEditor != null && !sceneEditor.IsDisposed && sceneEditor.Visible) sceneEditor.RefreshView();
                if (objectEditor != null && !objectEditor.IsDisposed && objectEditor.Visible) objectEditor.RefreshView();
            });
            MainMenuStrip.Items.Add(animation.Menu);
            animation.ResourceRootRequested += ShowEffectResources;
            sceneLoader = new ScenePreviewLoader(this);
            sceneLoader.Ready += renderer =>
            {
                savedSceneFingerprint = sceneLoader.InitialFingerprint;
                exportScenePng.Enabled = true;
                animation.Bind(renderer);
                sceneEditor = new SceneAuthoringForm(renderer);
                var page = new TabPage("地圖場景製作");
                sceneEditor.TopLevel = false; sceneEditor.FormBorderStyle = FormBorderStyle.None;
                sceneEditor.Dock = DockStyle.Fill; page.Controls.Add(sceneEditor);
                tabControl1.TabPages.Add(page); sceneEditor.Show();
                sceneEditor.Edited += (s, e) =>
                {
                    Text = Caption + " - " + document.Md3Path + " *";
                    foreach (var editor in collisionEditors) editor.Canvas.Invalidate();
                    objectEditor?.RefreshView();
                };
                objectEditor = new SceneObjectEditorForm(renderer);
                var objectPage = new TabPage("場景物件");
                objectEditor.TopLevel = false; objectEditor.FormBorderStyle = FormBorderStyle.None;
                objectEditor.Dock = DockStyle.Fill; objectPage.Controls.Add(objectEditor);
                tabControl1.TabPages.Add(objectPage); objectEditor.Show();
                objectEditor.Edited += (s, e) =>
                {
                    Text = Caption + " - " + document.Md3Path + " *";
                    foreach (var editor in collisionEditors) editor.Canvas.Invalidate();
                    sceneEditor.RefreshView();
                };
            };
            Disposed += (s, e) => { animation.Dispose(); sceneLoader.Dispose(); };
            fileMenu.Text = Strings.File; openToolStripMenuItem.Text = Strings.FileOpen;
            saveAsToolStripMenuItem.Text = Strings.SaveAs + " bundle...";
            exitToolStripMenuItem.Text = Strings.FileExit; languageToolStripMenuItem.Text = Strings.Language;
            this.SetFonts();
            var createMap = new ToolStripMenuItem("新建空白地圖（使用目前素材）…");
            createMap.Click += CreateBlankMap;
            fileMenu.DropDownItems.Insert(0, createMap);
            var resourceArchive = new ToolStripMenuItem("匯出完整遊戲資源包 ZIP…");
            resourceArchive.Click += (s, e) => ExportResourceArchive(); fileMenu.DropDownItems.Add(resourceArchive);
            AttachScenePngExport();
        }

        public void OpenMap(string fileName) { OpenMap(fileName, true); }

        private void OpenMap(string fileName, bool askToDiscard)
        {
            if (askToDiscard && !ConfirmDiscard()) return;
            var next = new MapDocumentService().Load(fileName);
            exportScenePng.Enabled = false;
            animation.Bind(null);
            while (tabControl1.TabPages.Count > 0)
            {
                var page = tabControl1.TabPages[0]; tabControl1.TabPages.RemoveAt(0); page.Dispose();
            }
            document = next;
            sceneEditor = null;
            objectEditor = null;
            collisionEditors = MapEditorTabs.Populate(tabControl1, next);
            savedFingerprint = MapEditFingerprint.Compute(next.Map);
            savedSceneFingerprint = null;
            foreach (var editor in collisionEditors)
                editor.Edited += (s, e) => Text = Caption + " - " + fileName +
                    (savedFingerprint != MapEditFingerprint.Compute(next.Map) || (sceneEditor?.IsDirty ?? false) || (objectEditor?.IsDirty ?? false) ? " *" : "");
            Text = Caption + " - " + fileName;
            sceneLoader.Load(next, collisionEditors);
        }

        private bool ConfirmDiscard()
        {
            sceneEditor?.FinishInteraction();
            objectEditor?.FinishInteraction();
            if (document == null) return true;
            bool sceneChanged = sceneLoader.Current != null && savedSceneFingerprint != null &&
                savedSceneFingerprint != MapEditFingerprint.ComputeScene(sceneLoader.Current.Scene);
            if (savedFingerprint == MapEditFingerprint.Compute(document.Map) && !sceneChanged) return true;
            return MessageBox.Show(this, "Discard unsaved map edits? Use Save As to export a new bundle.",
                "Unsaved map", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;
        }

        private void OpenFile(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog { Filter = Strings.OpenType + "|*.md3|All Files (*.*)|*.*" })
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    try { OpenMap(dialog.FileName); }
                    catch (Exception ex) { MessageBox.Show(this, ex.Message, "Cannot open map", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void ExitToolsStripMenuItem_Click(object sender, EventArgs e) { Close(); }

        private void CreateBlankMap(object sender, EventArgs e)
        {
            if (document == null)
            { MessageBox.Show(this, "請先開啟一張地圖作為地磚素材來源。", "新建空白地圖"); return; }
            if (!ConfirmDiscard()) return;
            using (var dimensions = new NewMapDialog())
            {
                if (dimensions.ShowDialog(this) != DialogResult.OK) return;
                using (var output = new SaveFileDialog { Filter = "MDN 地圖|*.md3", FileName = "new_map.md3" })
                {
                    if (output.ShowDialog(this) != DialogResult.OK) return;
                    try
                    {
                        var blank = new BlankMapService().Create(document, dimensions.MapWidth, dimensions.MapHeight);
                        new MapBundleExportService().Export(blank.Document, output.FileName, blank.Scene, false);
                        // The discard prompt was already handled before creation.
                        OpenMap(output.FileName, false);
                    }
                    catch (Exception ex) { MessageBox.Show(this, ex.Message, "新建地圖失敗", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                }
            }
        }

        public IEnumerable<CultureInfo> GetSupportedCultures()
        {
            string location = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            return CultureInfo.GetCultures(CultureTypes.AllCultures)
                .Where(culture => Directory.Exists(Path.Combine(location, culture.Name)));
        }

        private void MDIParent_Load(object sender, EventArgs e)
        {
            foreach (var culture in GetSupportedCultures())
            {
                var language = new ToolStripMenuItem(culture.TwoLetterISOLanguageName == "iv" ? "English" : culture.NativeName);
                language.Click += (s, args) =>
                {
                    if (!ConfirmDiscard()) return;
                    changedLanguage = true;
                    Thread.CurrentThread.CurrentCulture = culture; Thread.CurrentThread.CurrentUICulture = culture;
                    new MDIParent().Show(); Close();
                };
                languageToolStripMenuItem.DropDownItems.Add(language);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!changedLanguage && e.CloseReason != CloseReason.WindowsShutDown && !ConfirmDiscard())
            { e.Cancel = true; return; }
            base.OnFormClosing(e);
            if (!e.Cancel && !changedLanguage && e.CloseReason != CloseReason.WindowsShutDown) Application.Exit();
        }

        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (document == null) return;
            using (var dialog = new SaveFileDialog { Filter = Strings.OpenType + "|*.md3", FileName = Path.GetFileNameWithoutExtension(document.Md3Path) + "_edited.md3" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    sceneEditor?.FinishInteraction();
                    objectEditor?.FinishInteraction();
                    new MapBundleExportService().Export(document, dialog.FileName, sceneLoader.Current?.Scene);
                    savedFingerprint = MapEditFingerprint.Compute(document.Map);
                    if (sceneLoader.Current != null)
                        savedSceneFingerprint = MapEditFingerprint.ComputeScene(sceneLoader.Current.Scene);
                    foreach (var editor in collisionEditors) editor.MarkSaved();
                    sceneEditor?.MarkSaved();
                    objectEditor?.MarkSaved();
                    Text = Caption + " - exported " + dialog.FileName;
                }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "Export failed", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }
    }
}
