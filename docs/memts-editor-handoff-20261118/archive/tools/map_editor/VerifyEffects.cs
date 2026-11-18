using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using TricksterMap;
using TricksterMap.Effects;
using TricksterMap.SceneView;

internal static class VerifyEffects
{
    private static int Main(string[] args)
    {
        try
        {
            string source = Path.GetFullPath(args[0]), output = Path.GetFullPath(args[1]);
            if (Directory.Exists(output)) throw new IOException("Output must be new.");
            Directory.CreateDirectory(output);
            string before = Hash(source);
            var document = new MapDocumentService().Load(source);
            using (var renderer = SceneRenderer.Load(document))
            {
                if (args.Length > 2) renderer.LoadEffectResources(args[2]);
                var place = document.Map.EffectObjects.Select(raw => new MapEffectInstance(raw)).First();
                var view = new Rectangle(0, 0, 640, 480);
                double left = 320 - place.X, top = 240 - place.Y;
                using (var plain = new Bitmap(view.Width, view.Height, PixelFormat.Format32bppArgb))
                using (var first = new Bitmap(view.Width, view.Height, PixelFormat.Format32bppArgb))
                using (var next = new Bitmap(view.Width, view.Height, PixelFormat.Format32bppArgb))
                {
                    renderer.AnimateObjects = true; renderer.AnimationTimeSeconds = 1; renderer.ShowEffects = false;
                    using (var g = Graphics.FromImage(plain)) renderer.Render(g, view, 1, left, top);
                    plain.Save(Path.Combine(output, "without-effects.png"));
                    renderer.ShowEffects = true; renderer.AnimateObjects = true; renderer.AnimationTimeSeconds = 1;
                    var clock = Stopwatch.StartNew();
                    using (var g = Graphics.FromImage(first)) renderer.Render(g, view, 1, left, top);
                    long milliseconds = clock.ElapsedMilliseconds;
                    first.Save(Path.Combine(output, "effects-1s.png"));
                    renderer.AnimationTimeSeconds = 1.5;
                    using (var g = Graphics.FromImage(next)) renderer.Render(g, view, 1, left, top);
                    next.Save(Path.Combine(output, "effects-1.5s.png"));
                    int changed = 0, animated = 0;
                    for (int y = 0; y < view.Height; y++) for (int x = 0; x < view.Width; x++)
                    { if (plain.GetPixel(x, y) != first.GetPixel(x, y)) changed++; if (first.GetPixel(x, y) != next.GetPixel(x, y)) animated++; }
                    if (changed == 0 || renderer.EffectParticleCount == 0) throw new Exception("Actual map EFF produced no visible pixels/particles.");
                    string archive = Path.Combine(output, "map_effect_test.zip");
                    new MapResourceArchiveService().Export(document, archive, renderer.Scene, renderer.ResourceRoot);
                    if (Hash(source) != before) throw new Exception("Preview/export modified source.");
                    File.WriteAllLines(Path.Combine(output, "verification.txt"), new[] { "PASS: actual EFF produces particles and changed pixels; portable archive exported; source unchanged.",
                        "Map: " + source, "Anchor: " + place.X + "," + place.Y, "Changed pixels: " + changed, "Animated pixels: " + animated,
                        "Particles: " + renderer.EffectParticleCount, "Render at1s ms: " + milliseconds, "Source SHA256: " + before }.Concat(renderer.Warnings));
                    Console.WriteLine("PASS: changed=" + changed + "; animated=" + animated + "; particles=" + renderer.EffectParticleCount + "; ms=" + milliseconds);
                }
            }
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static string Hash(string path)
    { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant(); }
}
