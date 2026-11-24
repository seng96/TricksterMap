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
                SceneView.ScenePlaneCompositingVerification.Run();
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
                    var collisionOverlay = form.Controls.OfType<ToolStrip>().SelectMany(bar => bar.Items.OfType<ToolStripButton>())
                        .Single(item => item.Text == "碰撞疊圖");
                    Require(!collisionOverlay.Checked && !form.Canvas.ShowCollision, "Fine preview starts with a collision tint.");
                    var layer = form.Edits.Layer;
                    var original = (byte[])layer.Data.Clone();
                    form.VerifyViewportInteraction();
                    form.Canvas.VerifyPointAndRangeGestures();
                    double zoom = form.Canvas.View.Zoom, ox = form.Canvas.View.OriginX, oy = form.Canvas.View.OriginY;
                    form.DetailedPreview = false; form.DetailedPreview = true;
                    Require(zoom == form.Canvas.View.Zoom && ox == form.Canvas.View.OriginX && oy == form.Canvas.View.OriginY,
                        "Preview mode switch changed the viewport.");
                    Require(original.SequenceEqual(layer.Data), "View interaction modified collision cells.");
                    form.BrushDiameter = 5; form.VerifyBrushStroke();
                    Require(collisionOverlay.Checked && form.Canvas.ShowCollision, "Collision painting did not reveal the overlay.");
                    Require(form.Edits.IsDirty && form.Edits.CanUndo, "Sized brush handler did not edit a stroke.");
                    var painted = (byte[])layer.Data.Clone();
                    Require(painted.Zip(original, (a, b) => a != b).Count(changed => changed) > 1, "Sized brush changed only one cell.");
                    form.Edits.Undo(); Require(original.SequenceEqual(layer.Data), "Undo did not restore original bytes.");
                    form.Edits.Redo(); Require(painted.SequenceEqual(layer.Data), "Redo did not restore the complete stroke.");
                    SaveControl(form, Path.Combine(output, "fine-preview.png"));
                    form.DetailedPreview = false; SaveControl(form, Path.Combine(output, "simple-preview.png"));
                    form.DetailedPreview = true; collisionOverlay.PerformClick();
                    Require(!form.Canvas.ShowCollision, "The collision toggle did not restore the clean scene.");
                    SaveControl(form, Path.Combine(output, "map-style.png"));
                    VerifyMapOverlayControls(form, document.Map, output);
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
                    VerifyFullMapPng(main, output);
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
                        "PASS: hidden MDI; fine/simple modes; clean preview and collision overlay; additive planes and shadow multiplication; EFF menu toggle and resource diagnostics; anchor wheel zoom; middle/right/space pan; sized brush; undo/redo; point/range typed markers, hover, right-click edits, precise range frames, drag and resize; scene export/reload; image import; blank map; object drag/cancel/undo/redo; full-size scene PNG; source preservation\r\nSource SHA256: " + sourceHash + "\r\n" +
                        string.Join("\r\n", main.Preview.Warnings));
                }
                return 0;
            }
            catch (Exception ex)
            { if (Directory.Exists(output)) File.WriteAllText(Path.Combine(output, "verification-error.txt"), ex.ToString()); return 1; }
        }

        private static void VerifyMapOverlayControls(MapCollisionEditorForm form, Data.MapDataInfo map, string output)
        {
            var buttons = form.Controls.OfType<ToolStrip>().SelectMany(bar => bar.Items.OfType<ToolStripButton>()).ToArray();
            var point = buttons.Single(item => item.Text == "顯示點位");
            var range = buttons.Single(item => item.Text == "顯示範圍");
            var overlay = form.Canvas.ObjectOverlay;
            point.PerformClick();
            Require(overlay.ShowPoints && !overlay.ShowRanges, "Point visibility also changed range visibility.");
            range.PerformClick();
            Require(overlay.ShowPoints && overlay.ShowRanges, "Range visibility did not turn on independently.");
            SaveControl(form, Path.Combine(output, "point-range-overlay.png"));
            var view = form.Canvas.View;
            var onScreen = form.Canvas.ClientRectangle;
            onScreen.Inflate(-30, -30);
            foreach (var marker in map.PointObjects)
            {
                var pointer = new Point((int)Math.Round(view.ScreenX(marker.X)), (int)Math.Round(view.ScreenY(marker.Y)));
                if (!onScreen.Contains(pointer)) continue;
                overlay.SetHover(pointer);
                Require(overlay.HoverDetails.Length > 0, "A visible point did not activate its hover card.");
                SaveControl(form, Path.Combine(output, "point-hover.png"));
                break;
            }
            point.PerformClick();
            Require(!overlay.ShowPoints && overlay.ShowRanges, "Hiding points also hid ranges.");
            range.PerformClick();
            Require(!overlay.ShowRanges, "Range visibility did not turn off.");
        }

        private static void VerifyFullMapPng(MDIParent main, string output)
        {
            var menu = main.MainMenuStrip.Items.OfType<ToolStripMenuItem>().SelectMany(item => item.DropDownItems.OfType<ToolStripMenuItem>())
                .Single(item => item.Text == "匯出全圖 PNG…");
            Require(menu.Enabled, "PNG export menu is unavailable for the loaded scene.");
            var renderer = main.Preview;
            string fingerprint = MapEditFingerprint.ComputeScene(renderer.Scene);
            string path = Path.Combine(output, "full-map.png");
            main.ExportCurrentScenePng(path);
            using (var saved = new Bitmap(path))
            {
                Require(saved.Width == renderer.Scene.Width && saved.Height == renderer.Scene.Height,
                    "PNG export used viewport dimensions instead of map dimensions.");
                foreach (int top in new[] { 0, saved.Height / 2, saved.Height - 1 })
                {
                    using (var sample = new Bitmap(saved.Width, 1))
                    {
                        using (var graphics = Graphics.FromImage(sample))
                            renderer.Render(graphics, new Rectangle(0, 0, sample.Width, 1), 1, 0, -top);
                        for (int x = 0; x < saved.Width; x++)
                            Require(sample.GetPixel(x, 0) == saved.GetPixel(x, top), "PNG pixels differ from the scene renderer at 1:1.");
                    }
                }
            }
            Require(MapEditFingerprint.ComputeScene(renderer.Scene) == fingerprint, "PNG export modified the scene.");
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
