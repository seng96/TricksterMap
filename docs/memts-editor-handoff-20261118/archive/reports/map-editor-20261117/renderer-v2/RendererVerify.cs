using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Windows.Forms;
using TricksterMap;
using TricksterMap.Scene;
using TricksterMap.SceneEditing;
using TricksterMap.SceneView;

internal static class RendererVerify
{
    private static string Hash(string path)
    { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", ""); }

    public static int Main(string[] args)
    {
        try
        {
            PixelChecks();
            if (args.Length != 2) throw new ArgumentException("map.md3 output-folder");
            Directory.CreateDirectory(args[1]);
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            string original = Hash(args[0]);
            using (var renderer = SceneRenderer.Load(new MapDocumentService().Load(args[0])))
            {
                double zoom = Math.Min(1, Math.Min(1200.0 / renderer.Scene.Width, 760.0 / renderer.Scene.Height));
                Render(renderer, Path.Combine(args[1], "overview.png"), zoom, 0, 0);
                Render(renderer, Path.Combine(args[1], "detail.png"), 1,
                    600 - renderer.Scene.Width / 2.0, 380 - renderer.Scene.Height / 2.0);
                renderer.AnimateObjects = true; renderer.AnimationTimeSeconds = 0.25;
                Render(renderer, Path.Combine(args[1], "animation.png"), 1,
                    600 - renderer.Scene.Width / 2.0, 380 - renderer.Scene.Height / 2.0);
                if (renderer.Scene.Layers.Any(layer => layer.Kind == 2)) VerifyAuthoring(renderer, args[1]);
                if (renderer.Scene.Layers.Any(layer => layer.Objects.Count > 0)) VerifyObjects(renderer, args[1]);
                File.WriteAllLines(Path.Combine(args[1], "verification.txt"), new[] {
                    "PASS: C# Core scene load, overview/detail/animation viewport render, source SHA unchanged",
                    "MDN SHA256: " + original, "Scene size: " + renderer.Scene.Width + "x" + renderer.Scene.Height,
                    "Animated objects: " + renderer.HasAnimatedObjects }.Concat(renderer.Warnings));
                if (Hash(args[0]) != original) throw new InvalidOperationException("Source changed.");
            }
            VerifyBlank(new MapDocumentService().Load(args[0]), args[1]);
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    private static void VerifyBlank(MapDocument template, string output)
    {
        var blank = new BlankMapService().Create(template, 256, 256);
        using (var renderer = new SceneRenderer(blank.Scene))
        using (var form = new SceneObjectEditorForm(renderer))
        {
            if (!renderer.AvailableObjectNames.Any()) throw new InvalidOperationException("Empty new map did not expose existing BAC assets.");
            if (blank.Scene.Layers.Any(layer => layer.Objects.Count != 0)) throw new InvalidOperationException("Blank map starts with objects.");
            form.Size = new Size(1000, 700); form.PrepareVerificationView(); form.VerifyAddObject();
            if (blank.Scene.Layers[1].Objects.Count != 1) throw new InvalidOperationException("New object was not placed in the default Main layer.");
            using (var image = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
                image.Save(Path.Combine(output, "blank-object-placement.png"), ImageFormat.Png);
            }
            string directory = Path.Combine(output, "blank-export-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory); string exported = Path.Combine(directory, "authored.md3");
            new MapBundleExportService().Export(blank.Document, exported, blank.Scene, false);
            var reopened = SceneDocument.Load(new MapDocumentService().Load(exported));
            if (reopened.Layers[1].Objects.Count != 1 || !blank.Scene.Layers[1].Objects[0].RawData.SequenceEqual(reopened.Layers[1].Objects[0].RawData))
                throw new InvalidOperationException("New blank object differs after export/reopen.");
            File.WriteAllText(Path.Combine(output, "blank-object-verification.txt"),
                "PASS: empty blank Main layer / BAC asset catalog / new zero record with original name bytes / add undo redo / export and exact record reopen\r\n");
        }
    }

    private static void VerifyObjects(SceneRenderer renderer, string output)
    {
        using (var form = new SceneObjectEditorForm(renderer))
        {
            form.Size = new Size(1280, 820); form.PrepareVerificationView(); form.VerifyObjectEdit();
            if (!form.IsDirty) throw new InvalidOperationException("Object authoring did not become dirty.");
            using (var image = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
                image.Save(Path.Combine(output, "object-editor.png"), ImageFormat.Png);
            }
            string directory = Path.Combine(output, "object-export-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory); string exported = Path.Combine(directory, "verified.md3");
            new MapBundleExportService().Export(renderer.Scene.Document, exported, renderer.Scene);
            var reopened = SceneDocument.Load(new MapDocumentService().Load(exported));
            for (int i = 0; i < renderer.Scene.Layers.Count; i++)
            {
                if (renderer.Scene.Layers[i].Objects.Count != reopened.Layers[i].Objects.Count)
                    throw new InvalidOperationException("Object count differs after export/reopen.");
                for (int j = 0; j < reopened.Layers[i].Objects.Count; j++)
                    if (!renderer.Scene.Layers[i].Objects[j].RawData.SequenceEqual(reopened.Layers[i].Objects[j].RawData))
                        throw new InvalidOperationException("Object raw record differs after export/reopen.");
            }
            form.MarkSaved(); if (form.IsDirty) throw new InvalidOperationException("Object authoring save marker failed.");
            File.WriteAllText(Path.Combine(output, "object-verification.txt"),
                "PASS: object coordinate apply / undo / redo / duplicate raw record / delete / undo / bundle export / full record reopen / saved state / offscreen UI\r\n");
        }
    }

    private static void VerifyAuthoring(SceneRenderer renderer, string output)
    {
        using (var form = new SceneAuthoringForm(renderer))
        {
            form.Size = new Size(1280, 820); form.PrepareVerificationView(); form.VerifyTileStroke();
            if (!form.IsDirty) throw new InvalidOperationException("Tile authoring did not become dirty.");
            using (var image = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
                image.Save(Path.Combine(output, "tile-authoring.png"), ImageFormat.Png);
            }
            string directory = Path.Combine(output, "tile-export-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory); string exported = Path.Combine(directory, "verified.md3");
            new MapBundleExportService().Export(renderer.Scene.Document, exported, renderer.Scene);
            var reopened = SceneDocument.Load(new MapDocumentService().Load(exported));
            for (int i = 0; i < renderer.Scene.Layers.Count; i++)
                if (!renderer.Scene.Layers[i].Payload.SequenceEqual(reopened.Layers[i].Payload))
                    throw new InvalidOperationException("Edited tile payload differs after bundle export/reopen.");
            form.MarkSaved(); if (form.IsDirty) throw new InvalidOperationException("Tile authoring save marker failed.");
            File.WriteAllText(Path.Combine(output, "tile-verification.txt"),
                "PASS: real tile canvas mouse handler / undo / redo / shared scene export / reopen / saved state / offscreen UI\r\n");
        }
    }

    private static void PixelChecks()
    {
        // The disk's first row is the display's bottom row. Non-symmetric
        // colors make a mistaken vertical orientation or flip observable.
        byte[] tile = { 0x1f, 0x00, 0xff, 0x7f, 0x00, 0x7c, 0xe0, 0x03 };
        Color[][] expected = {
            new[] { Color.Red, Color.Lime, Color.Blue, Color.White },
            new[] { Color.Lime, Color.Red, Color.White, Color.Blue },
            new[] { Color.Blue, Color.White, Color.Red, Color.Lime },
            new[] { Color.White, Color.Blue, Color.Lime, Color.Red }
        };
        for (byte flip = 0; flip < 4; flip++)
            using (var image = ScenePixels.Tile(tile, 0, 2, 2, flip))
                for (int y = 0; y < 2; y++) for (int x = 0; x < 2; x++)
                    if (image.GetPixel(x, y).ToArgb() != expected[flip][y * 2 + x].ToArgb())
                        throw new InvalidOperationException("Asymmetric tile flip/orientation failed: " + flip);
        using (var image = ScenePixels.Tile(tile, 0, 2, 2, 0x20))
            if (image.GetPixel(0, 0).A != 160) throw new InvalidOperationException("Tile color alpha differs.");
        using (var image = ScenePixels.Tile(new byte[] { 0, 0x10 }, 0, 1, 1, 0))
            if (image.GetPixel(0, 0).R != 32) throw new InvalidOperationException("RGB555 normalized channel differs.");
        // Three-pixel bottom row blue/white/yellow; top row skip/red/skip.
        byte[] rle = { 12, 0, 0, 0, 3, 0, 0x1f, 0, 0xff, 0x7f, 0xe0, 0x7f,
                       8, 0, 1, 0, 1, 0, 0, 0x7c };
        using (var cropped = new SceneBitmapSource(rle, 3, 2, true).Decode(new Rectangle(1, 0, 2, 2)))
        {
            if (cropped.GetPixel(0, 0).ToArgb() != Color.Red.ToArgb() || cropped.GetPixel(1, 0).A != 0 ||
                cropped.GetPixel(0, 1).ToArgb() != Color.White.ToArgb() || cropped.GetPixel(1, 1).ToArgb() != Color.Yellow.ToArgb())
                throw new InvalidOperationException("Bottom-up bitmap RLE crop differs.");
        }
    }

    private static void Render(SceneRenderer renderer, string path, double zoom, double originX, double originY)
    {
        using (var image = new Bitmap(1200, 760))
        using (var graphics = Graphics.FromImage(image))
        {
            renderer.Render(graphics, new Rectangle(0, 0, image.Width, image.Height), zoom, originX, originY);
            image.Save(path, ImageFormat.Png);
        }
    }
}
