using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Web.Script.Serialization;
using TricksterMap;
using TricksterMap.Effects;
using TricksterMap.SceneView;

internal static class AuditEffects
{
    private static int Main(string[] args)
    {
        string corpus = Path.GetFullPath(args[0]), output = Path.GetFullPath(args[1]);
        if (Directory.Exists(output)) throw new IOException("Output must be new."); Directory.CreateDirectory(output);
        var rows = new List<Dictionary<string, object>>(); var clock = Stopwatch.StartNew();
        foreach (string effect in Directory.GetFiles(corpus, "*.eff").OrderBy(p => p))
        {
            var row = new Dictionary<string, object> { { "map", Path.GetFileNameWithoutExtension(effect) } }; rows.Add(row);
            string map = Path.ChangeExtension(effect, ".md3");
            if (!File.Exists(map)) { row["status"] = "no-mdn"; continue; }
            try
            {
                string hash = Hash(map), effHash = Hash(effect); var document = new MapDocumentService().Load(map);
                using (var renderer = SceneRenderer.Load(document))
                {
                    if (args.Length > 2) renderer.LoadEffectResources(args[2]);
                    row["placements"] = document.Map.EffectObjects.Count;
                    var dependencies = renderer.EffectDependencies;
                    row["material"] = dependencies != null && dependencies.Material != null ? dependencies.Material.Status : "no-document";
                    row["dependency_errors"] = dependencies == null || dependencies.HasBlockingIssues;
                    var anchor = document.Map.EffectObjects.Count > 0 ? new MapEffectInstance(document.Map.EffectObjects[0]) : null;
                    double left = 160 - (anchor == null ? document.Map.MapSizeX / 2 : anchor.X), top = 120 - (anchor == null ? document.Map.MapSizeY / 2 : anchor.Y);
                    using (var plain = new Bitmap(320, 240))
                    using (var rendered = new Bitmap(320, 240))
                    {
                        renderer.AnimateObjects = true; renderer.AnimationTimeSeconds = .5; renderer.ShowEffects = false;
                        using (var graphics = Graphics.FromImage(plain)) renderer.Render(graphics, new Rectangle(0, 0, 320, 240), 1, left, top);
                        renderer.ShowEffects = true; var timer = Stopwatch.StartNew();
                        using (var graphics = Graphics.FromImage(rendered)) renderer.Render(graphics, new Rectangle(0, 0, 320, 240), 1, left, top);
                        row["milliseconds"] = timer.ElapsedMilliseconds; row["particles"] = renderer.EffectParticleCount;
                        int changed = 0;
                        for (int y = 0; y < 240; y++) for (int x = 0; x < 320; x++) if (plain.GetPixel(x, y) != rendered.GetPixel(x, y)) changed++;
                        row["changed_pixels"] = changed;
                        if (changed > 0 && rows.Count % 10 == 0) rendered.Save(Path.Combine(output, Path.GetFileNameWithoutExtension(effect) + ".png"));
                    }
                    row["warnings"] = renderer.Warnings.ToArray(); row["source_preserved"] = Hash(map) == hash && Hash(effect) == effHash;
                    row["status"] = "rendered";
                }
            }
            catch (Exception error) { row["status"] = "error"; row["error"] = error.ToString(); }
            if (rows.Count % 10 == 0) { GC.Collect(); Console.WriteLine("Processed " + rows.Count); }
        }
        var summary = new { elapsed_seconds = clock.Elapsed.TotalSeconds, count = rows.Count, rendered = rows.Count(r => (string)r["status"] == "rendered"),
            errors = rows.Count(r => (string)r["status"] == "error"), visible_effects = rows.Count(r => r.ContainsKey("changed_pixels") && (int)r["changed_pixels"] > 0),
            active_particles = rows.Count(r => r.ContainsKey("particles") && (int)r["particles"] > 0),
            all_sources_preserved = rows.Where(r => r.ContainsKey("source_preserved")).All(r => (bool)r["source_preserved"]), rows };
        var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        File.WriteAllText(Path.Combine(output, "audit.json"), serializer.Serialize(summary));
        Console.WriteLine("Finished " + rows.Count + " in " + clock.Elapsed.TotalSeconds.ToString("F2") + " seconds; errors=" + summary.errors);
        return summary.errors == 0 && summary.all_sources_preserved ? 0 : 1;
    }
    private static string Hash(string path)
    { using (var hash = SHA256.Create()) using (var input = File.OpenRead(path)) return BitConverter.ToString(hash.ComputeHash(input)); }
}
