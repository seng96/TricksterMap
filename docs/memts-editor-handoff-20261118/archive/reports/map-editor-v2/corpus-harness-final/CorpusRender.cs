using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using TricksterMap;
using TricksterMap.SceneView;

internal static class CorpusRender
{
    private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
    private const int Width = 320, Height = 240;
    private static string Hash(string path)
    {
        using (var sha = SHA256.Create()) using (var input = File.OpenRead(path))
            return BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
    }

    public static int Main(string[] args)
    {
        if (args.Length < 3) { Console.Error.WriteLine("maps-folder output-folder data-map-sq14a"); return 2; }
        Directory.CreateDirectory(args[1]);
        var files = Directory.EnumerateFiles(args[0], "*.md3").OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        files.Add(args[2]);
        var rows = new List<Dictionary<string, object>>();
        int complete = 0, failed = 0, skipped = 0;
        var total = Stopwatch.StartNew();
        using (var progress = new StreamWriter(Path.Combine(args[1], "rows.ndjson"), false, new UTF8Encoding(false)))
        {
            progress.AutoFlush = true;
            foreach (string path in files)
            {
                byte[] head = new byte[4];
                using (var stream = File.OpenRead(path)) stream.Read(head, 0, head.Length);
                if (Encoding.ASCII.GetString(head) != "MDN_") { skipped++; continue; }
                bool missing = new[] { ".bac", ".lyr", ".til" }.Any(ext => !File.Exists(Path.ChangeExtension(path, ext)));
                if (missing) { skipped++; continue; }
                var row = new Dictionary<string, object> { { "path", path }, { "source_sha256_before", Hash(path) } };
                var clock = Stopwatch.StartNew();
                try
                {
                    using (var renderer = SceneRenderer.Load(new MapDocumentService().Load(path)))
                    using (var image = new Bitmap(Width, Height, PixelFormat.Format32bppArgb))
                    using (var graphics = Graphics.FromImage(image))
                    {
                        row["load_ms"] = clock.Elapsed.TotalMilliseconds;
                        double zoom = Math.Min((Width - 8.0) / renderer.Scene.Width, (Height - 8.0) / renderer.Scene.Height);
                        double x = (Width - renderer.Scene.Width * zoom) / 2;
                        double y = (Height - renderer.Scene.Height * zoom) / 2;
                        renderer.Render(graphics, new Rectangle(0, 0, Width, Height), zoom, x, y);
                        var pixels = new int[Width * Height];
                        var bits = image.LockBits(new Rectangle(0, 0, Width, Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                        try { Marshal.Copy(bits.Scan0, pixels, 0, pixels.Length); }
                        finally { image.UnlockBits(bits); }
                        int background = Color.DarkSlateGray.ToArgb();
                        int nonBackground = pixels.Count(value => value != background);
                        int unique = pixels.Distinct().Count();
                        row["status"] = "rendered"; row["map_width"] = renderer.Scene.Width; row["map_height"] = renderer.Scene.Height;
                        row["layer_count"] = renderer.Scene.Layers.Count;
                        row["object_count"] = renderer.Scene.Layers.Sum(layer => layer.Objects.Count);
                        row["tile_group_count"] = renderer.Scene.TileGroups.Count;
                        row["zoom"] = zoom; row["origin_x"] = x; row["origin_y"] = y;
                        row["non_background_pixels"] = nonBackground; row["unique_pixel_colors"] = unique;
                        row["warnings"] = renderer.Warnings.ToArray(); row["animated_objects"] = renderer.HasAnimatedObjects;
                        bool selected = Path.GetFileName(path).Equals("map_sq14a.md3", StringComparison.OrdinalIgnoreCase) ||
                            Path.GetFileName(path).Equals("map_sq00.md3", StringComparison.OrdinalIgnoreCase);
                        if (unique <= 1 || selected)
                        {
                            string imageName = (rows.Count + 1).ToString("D4") + "-" + Path.GetFileNameWithoutExtension(path) + ".png";
                            image.Save(Path.Combine(args[1], imageName), ImageFormat.Png); row["image"] = imageName;
                        }
                    }
                    complete++;
                }
                catch (Exception ex) { row["status"] = "failed"; row["error"] = ex.ToString(); failed++; }
                row["elapsed_ms"] = clock.Elapsed.TotalMilliseconds;
                row["source_sha256_after"] = Hash(path);
                row["source_unchanged"] = row["source_sha256_after"].Equals(row["source_sha256_before"]);
                rows.Add(row); progress.WriteLine(Json.Serialize(row));
                if (rows.Count % 25 == 0)
                {
                    Console.WriteLine("Rendered " + complete + ", failed " + failed + ", elapsed " + total.Elapsed.TotalSeconds.ToString("0.0") + "s");
                    GC.Collect(); GC.WaitForPendingFinalizers();
                }
            }
        }
        string source = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TricksterMap.Core.dll");
        var report = new Dictionary<string, object> {
            { "generated_utc", DateTime.UtcNow.ToString("o") }, { "viewport", new[] { Width, Height } },
            { "policy", "Complete MDN root files; fit whole scene, first frame, no game runtime or animation comparison." },
            { "core_dll_sha256", Hash(source) }, { "renderer_dll_sha256", Hash(typeof(SceneRenderer).Assembly.Location) },
            { "input_count", files.Count }, { "rendered", complete }, { "failed", failed }, { "skipped", skipped },
            { "elapsed_seconds", total.Elapsed.TotalSeconds }, { "rows", rows }
        };
        File.WriteAllText(Path.Combine(args[1], "corpus-render.json"), Json.Serialize(report), new UTF8Encoding(false));
        Console.WriteLine("FINISHED " + complete + " rendered; " + failed + " failed; " + skipped + " skipped.");
        return failed == 0 && rows.All(r => (bool)r["source_unchanged"]) ? 0 : 1;
    }
}
