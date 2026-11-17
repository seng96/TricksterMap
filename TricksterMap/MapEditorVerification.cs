using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Forms;
using TricksterMap.Scene;
using TricksterMap.SceneEditing;

namespace TricksterMap
{
    internal static class MapEditorVerification
    {
        public static int Run(string[] args)
        {
            if (args.Length != 4 || args[2] != "--output") return 1;
            string output = Path.GetFullPath(args[3]);
            try
            {
                if (Directory.Exists(output) || File.Exists(output)) throw new IOException("Verification output must be new.");
                Directory.CreateDirectory(output);
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                string sourceHash = Hash(args[1]);
                using (var main = new MDIParent())
                {
                    main.PrepareVerificationView(); main.OpenMap(args[1]);
                    var timeout = Stopwatch.StartNew();
                    while (main.Preview == null && timeout.ElapsedMilliseconds < 60000)
                    { Application.DoEvents(); Thread.Sleep(10); }
                    Require(main.Preview != null, "Fine scene preview did not load within 60 seconds.");
                    var effectsMenu = main.MainMenuStrip.Items.OfType<ToolStripMenuItem>().Single(item => item.Text == "預覽")
                        .DropDownItems.OfType<ToolStripMenuItem>().Single(item => item.Text == "顯示地圖 EFF 特效");
                    bool effectsBefore = effectsMenu.Checked;
                    effectsMenu.PerformClick(); Require(main.Preview.ShowEffects == !effectsBefore, "EFF menu did not toggle rendering.");
                    effectsMenu.PerformClick(); Require(main.Preview.ShowEffects == effectsBefore, "EFF menu did not restore rendering.");
                    using (var resources = new EffectResourcesForm(main.Preview, () => { }))
                    {
                        resources.TopLevel = false; main.Controls.Add(resources); resources.Show();
                        Require(resources.Controls.OfType<TextBox>().Single().Visible, "Resource diagnostics did not become visible.");
                        SaveControl(resources, Path.Combine(output, "effect-resources.png"));
                    }
                    var document = main.EditingDocument;
                    var form = main.CollisionEditors.First();
                    var layer = form.Edits.Layer;
                    var original = (byte[])layer.Data.Clone();
                    form.VerifyViewportInteraction();
                    double zoom = form.Canvas.View.Zoom, ox = form.Canvas.View.OriginX, oy = form.Canvas.View.OriginY;
                    form.DetailedPreview = false; form.DetailedPreview = true;
                    Require(zoom == form.Canvas.View.Zoom && ox == form.Canvas.View.OriginX && oy == form.Canvas.View.OriginY,
                        "Preview mode switch changed the viewport.");
                    Require(original.SequenceEqual(layer.Data), "View interaction modified collision cells.");
                    form.BrushDiameter = 5; form.VerifyBrushStroke();
                    Require(form.Edits.IsDirty && form.Edits.CanUndo, "Sized brush handler did not edit a stroke.");
                    var painted = (byte[])layer.Data.Clone();
                    Require(painted.Zip(original, (a, b) => a != b).Count(changed => changed) > 1, "Sized brush changed only one cell.");
                    form.Edits.Undo(); Require(original.SequenceEqual(layer.Data), "Undo did not restore original bytes.");
                    form.Edits.Redo(); Require(painted.SequenceEqual(layer.Data), "Redo did not restore the complete stroke.");
                    SaveControl(form, Path.Combine(output, "fine-preview.png"));
                    form.DetailedPreview = false; SaveControl(form, Path.Combine(output, "simple-preview.png"));
                    form.DetailedPreview = true; form.Canvas.ShowCollision = false;
                    SaveControl(form, Path.Combine(output, "map-style.png")); form.Canvas.ShowCollision = true;
                    SaveControl(main, Path.Combine(output, "workspace.png"));
                    var author = main.Controls.OfType<TabControl>().First().TabPages.Cast<TabPage>()
                        .SelectMany(page => page.Controls.OfType<SceneAuthoringForm>()).First();
                    if (main.Preview.Scene.Layers.Any(sceneLayer => sceneLayer.Kind == 2))
                    {
                        author.VerifyTileStroke();
                        SaveControl(author, Path.Combine(output, "tile-authoring.png"));
                    }
                    var objectAuthor = main.Controls.OfType<TabControl>().First().TabPages.Cast<TabPage>()
                        .SelectMany(page => page.Controls.OfType<SceneObjectEditorForm>()).First();
                    if (main.Preview.Scene.Layers.Any(sceneLayer => sceneLayer.Objects.Count > 0))
                    {
                        objectAuthor.VerifyObjectEdit();
                        SaveControl(objectAuthor, Path.Combine(output, "object-authoring.png"));
                    }
                    string exported = Path.Combine(output, "verified.md3");
                    new MapBundleExportService().Export(document, exported, main.Preview.Scene);
                    var reopened = new MapDocumentService().Load(exported);
                    Require(painted.SequenceEqual(reopened.Map.ConfigLayers.First(c => c.Type == 1).Data), "Exported collision differs.");
                    var reopenedScene = SceneDocument.Load(reopened);
                    Require(main.Preview.Scene.SerializeLayers().SequenceEqual(reopenedScene.SerializeLayers()), "Exported scene differs.");
                    foreach (var extension in new[] { ".bac", ".til", ".eff" })
                    {
                        string source = Path.ChangeExtension(document.Md3Path, extension);
                        if (File.Exists(source)) Require(Hash(source) == Hash(Path.ChangeExtension(exported, extension)), "Unedited companion differs: " + extension);
                    }
                    SceneAuthoringVerification.Run(document, output);
                    VerifyLatestLoad(document.Md3Path, Path.Combine(output, "blank-created.md3"));
                    Require(Hash(document.Md3Path) == sourceHash, "Verification modified the source map.");
                    form.MarkSaved(); author.MarkSaved(); objectAuthor.MarkSaved();
                    File.WriteAllText(Path.Combine(output, "verification.txt"),
                        "PASS: hidden MDI; fine/simple modes; EFF menu toggle and resource diagnostics; anchor wheel zoom; middle/right/space pan; sized brush; undo/redo; scene export/reload; image import; blank map; source preservation\r\nSource SHA256: " + sourceHash + "\r\n" +
                        string.Join("\r\n", main.Preview.Warnings));
                }
                return 0;
            }
            catch (Exception ex)
            { if (Directory.Exists(output)) File.WriteAllText(Path.Combine(output, "verification-error.txt"), ex.ToString()); return 1; }
        }

        internal static void SaveControl(Control control, string path)
        {
            var page = control.Parent as TabPage;
            var tabs = page?.Parent as TabControl;
            if (tabs != null) tabs.SelectedTab = page;
            control.Refresh(); Application.DoEvents();
            using (var bitmap = new Bitmap(control.Width, control.Height))
            { control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(path, ImageFormat.Png); }
        }
        private static void VerifyLatestLoad(string first, string last)
        {
            using (var window = new MDIParent())
            {
                window.PrepareVerificationView(); window.OpenMap(first); window.OpenMap(last);
                var timeout = Stopwatch.StartNew();
                while (window.Preview == null && timeout.ElapsedMilliseconds < 60000)
                { Application.DoEvents(); Thread.Sleep(10); }
                Require(window.Preview != null && window.Preview.Scene.Document.Md3Path == last,
                    "A stale scene worker replaced the latest map.");
            }
        }
        internal static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        internal static string Hash(string path)
        {
            using (var hash = SHA256.Create()) using (var stream = File.OpenRead(path))
                return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }
}
