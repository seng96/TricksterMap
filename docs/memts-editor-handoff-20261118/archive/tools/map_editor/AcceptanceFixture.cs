using System;
using System.Drawing;
using System.IO;
using System.Linq;
using TricksterMap;
using TricksterMap.Data;
using TricksterMap.Scene;
using TricksterMap.SceneView;

// Builds an offline acceptance bundle through the same Core services as the GUI.
// The chosen existing map keeps its gameplay entry, return, NPC and ground data.
// It does not deploy files, change registrations, restart servers or log in.
internal static class AcceptanceFixture
{
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length != 2) throw new ArgumentException("Usage: AcceptanceFixture <template.md3> <new-output-directory>");
            string output = Path.GetFullPath(args[1]);
            if (Directory.Exists(output) || File.Exists(output)) throw new IOException("Output must be new.");
            var original = new MapDocumentService().Load(Path.GetFullPath(args[0]));
            var sourceScene = SceneDocument.Load(original);
            var blank = new BlankMapService().Create(original, original.Map.MapSizeX + 1, original.Map.MapSizeY + 1);
            var map = blank.Document.Map;
            foreach (var layer in original.Map.ConfigLayers.Where(layer => layer.Type != 1))
                map.ConfigLayers.Add(new ConfigLayer { Type = layer.Type, X = layer.X, Y = layer.Y,
                    BppX = layer.BppX, BppY = layer.BppY, Data = (byte[])layer.Data.Clone() });
            foreach (var point in original.Map.PointObjects)
                map.PointObjects.Add(new PointObject { Type = point.Type, Id = point.Id, MapId = point.MapId,
                    X = point.X, Y = point.Y, Options = point.Options });
            foreach (var range in original.Map.RangeObjects)
                map.RangeObjects.Add(new RangeObject { Type = range.Type, Id = range.Id, Destination = range.Destination,
                    X1 = range.X1, Y1 = range.Y1, X2 = range.X2, Y2 = range.Y2, Options = range.Options });

            var collision = new ConfigLayerEditService(map.ConfigLayers[0]);
            collision.FillRectangle(2, 2, collision.Layer.X - 3, collision.Layer.Y - 3, 0);
            int wallX = collision.Layer.X / 2, wallY = collision.Layer.Y / 2;
            collision.FillRectangle(wallX, wallY - 12, wallX + 1, wallY + 12, 1);
            collision.FillRectangle(wallX, wallY + 1, wallX + 1, wallY + 4, 0);
            // Preserve usable arrivals and NPC positions from the registered map.
            foreach (var point in map.PointObjects)
                collision.PaintBrush(point.X / 16, point.Y / 16, 5, 0, false);
            foreach (var range in map.RangeObjects.Where(range => range.Type == 0))
                collision.FillRectangle(Math.Max(0, range.X1 / 16 - 2), Math.Max(0, range.Y1 / 16 - 2),
                    Math.Min(collision.Layer.X - 1, range.X2 / 16 + 2), Math.Min(collision.Layer.Y - 1, range.Y2 / 16 + 2), 0);

            int group = new SceneTileImportService().Append(blank.Scene, 4, 1, TilePixels());
            var tiles = new SceneTileEditService(blank.Scene, blank.Scene.Layers[0]);
            tiles.BeginStroke();
            for (int y = 0; y < tiles.Layer.Height; y++)
                for (int x = 0; x < tiles.Layer.Width; x++)
                {
                    bool blocked = Enumerable.Range(0, 4).Any(i => collision.Layer.Data[(y * 2 + i / 2) * collision.Layer.X + x * 2 + i % 2] == 1);
                    int tile = blocked ? 1 : (x + y) % 2 == 0 ? 0 : 2;
                    tiles.SetCell(x, y, (uint)(group << 16 | tile));
                }
            tiles.EndStroke();
            var sample = sourceScene.Layers.SelectMany(layer => layer.Objects).FirstOrDefault();
            if (sample != null)
            {
                var item = new SceneObject(sample.RawData) { X = wallX * 16 + 192, Y = wallY * 16, Z = 0 };
                new SceneObjectEditService(blank.Scene.Layers[1]).Add(item);
            }

            Directory.CreateDirectory(output);
            string path = Path.Combine(output, Path.GetFileName(original.Md3Path));
            new MapBundleExportService().Export(blank.Document, path, blank.Scene, false);
            var reopened = new MapDocumentService().Load(path);
            var scene = SceneDocument.Load(reopened);
            if (reopened.Map.PointObjects.Count != original.Map.PointObjects.Count ||
                reopened.Map.RangeObjects.Count != original.Map.RangeObjects.Count ||
                scene.Layers[0].Payload.Length != tiles.Layer.Payload.Length ||
                !scene.Layers[0].Payload.SequenceEqual(tiles.Layer.Payload))
                throw new InvalidDataException("Fixture reopen validation failed.");
            using (var renderer = new SceneRenderer(scene))
            using (var image = new Bitmap(940, 830))
            using (var graphics = Graphics.FromImage(image))
            {
                graphics.Clear(Color.FromArgb(30, 35, 40));
                renderer.Render(graphics, new Rectangle(0, 0, image.Width, image.Height), 940.0 / scene.Width, 0, 0);
                image.Save(Path.Combine(output, "preview.png"));
                if (renderer.Warnings.Any()) Console.WriteLine("Renderer warnings: " + string.Join("; ", renderer.Warnings));
            }
            File.WriteAllLines(Path.Combine(output, "fixture.txt"), new[] {
                "Offline acceptance fixture; in-game validation is pending.",
                "Registered target: 148; channel: 3 (Adventure Island 3); source: " + original.Md3Path,
                "Dimensions: " + scene.Width + "x" + scene.Height,
                "Original gameplay points/ranges and non-collision config layers retained.",
                "Independent imported checker tiles, red blocking border and center wall, one BAC object.",
                "Wall (world pixels): x=" + wallX * 16 + ".." + (wallX + 2) * 16 +
                    ", y=" + (wallY - 12) * 16 + ".." + (wallY + 13) * 16,
                "Wall opening: y=" + (wallY + 1) * 16 + ".." + (wallY + 5) * 16,
                "Walkable cells: " + collision.Layer.Data.Count(value => value == 0),
                "Blocked cells: " + collision.Layer.Data.Count(value => value == 1),
                "Point count: " + map.PointObjects.Count + "; range count: " + map.RangeObjects.Count,
                "Generated companions: MD3, LYR, TIL; byte-preserved template BAC; no template EFF."
            });
            File.WriteAllLines(Path.Combine(output, "routes.tsv"),
                new[] { "kind\ttype\tid\tdestination\tx1\ty1\tx2\ty2" }
                .Concat(map.PointObjects.Select(point => "point\t" + point.Type + "\t" + point.Id + "\t" + point.MapId + "\t" + point.X + "\t" + point.Y + "\t\t"))
                .Concat(map.RangeObjects.Select(range => "range\t" + range.Type + "\t" + range.Id + "\t" + range.Destination + "\t" + range.X1 + "\t" + range.Y1 + "\t" + range.X2 + "\t" + range.Y2)));
            Console.WriteLine("Created and reopened: " + path);
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static byte[] TilePixels()
    {
        var colors = new[] { Color.FromArgb(92, 138, 116), Color.FromArgb(150, 65, 65), Color.FromArgb(112, 158, 136), Color.Gold };
        var pixels = new byte[4 * 32 * 32 * 2];
        for (int tile = 0; tile < 4; tile++)
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    var color = x == 0 || y == 0 ? Color.FromArgb(56, 65, 62) : colors[tile];
                    ushort value = (ushort)((color.R >> 3) << 10 | (color.G >> 3) << 5 | color.B >> 3);
                    int offset = (tile * 1024 + (31 - y) * 32 + x) * 2;
                    pixels[offset] = (byte)value; pixels[offset + 1] = (byte)(value >> 8);
                }
        return pixels;
    }
}
