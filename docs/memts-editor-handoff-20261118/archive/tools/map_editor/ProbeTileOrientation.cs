using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using TricksterMap;
using TricksterMap.SceneView;

internal static class ProbeTileOrientation
{
    static int Main(string[] args)
    {
        Directory.CreateDirectory(args[1]);
        string[] sources = new[] { ".md3", ".lyr", ".til", ".bac", ".eff" }.Select(ext => Path.ChangeExtension(args[0], ext)).Where(File.Exists).ToArray();
        string[] hashes = sources.Select(Hash).ToArray();
        for (int variant = 0; variant < 4; variant++)
        using (var renderer = SceneRenderer.Load(new MapDocumentService().Load(args[0])))
        {
            renderer.ShowEffects = false;
            if (variant == 0) Console.WriteLine("Scene " + renderer.Scene.Width + "x" + renderer.Scene.Height + "; " + string.Join("; ", renderer.Scene.Layers.Select(l => l.Name + ":" + l.Kind + ":" + l.X + "," + l.Y)));
            foreach (var layer in renderer.Scene.Layers.Where(l => l.Kind == 2))
                for (int y = 0; y < layer.Height; y++) for (int x = 0; x < layer.Width; x++)
                    layer.SetTile(x, y, layer.GetTile(x, y) ^ ((uint)variant << 24));
            using (var bmp = new Bitmap(826, 532))
            {
                using (var g = Graphics.FromImage(bmp)) renderer.Render(g, new Rectangle(0, 0, bmp.Width, bmp.Height), 1, 413 - 1120, 266 - 1088);
                bmp.Save(Path.Combine(args[1], "orientation-" + variant + ".png"));
            }
        }
        if (!sources.Select(Hash).SequenceEqual(hashes)) throw new Exception("Source bundle changed.");
        File.WriteAllLines(Path.Combine(args[1], "verification.txt"), new[] { "PASS: orientation variants rendered; source bundle unchanged." }.Concat(sources.Select((s, i) => s + " SHA256 " + hashes[i])));
        return 0;
    }
    static string Hash(string path)
    { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant(); }
}
