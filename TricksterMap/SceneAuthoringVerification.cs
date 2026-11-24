using System.Drawing;
using System.IO;
using System.Linq;
using TricksterMap.Scene;
using TricksterMap.SceneEditing;
using TricksterMap.SceneView;

namespace TricksterMap
{
    internal static class SceneAuthoringVerification
    {
        internal static void Run(MapDocument source, string output)
        {
            var blank = new BlankMapService().Create(source, 256, 256);
            using (var renderer = new SceneRenderer(blank.Scene))
            using (var image = new Bitmap(64, 32))
            {
                using (var graphics = Graphics.FromImage(image))
                { graphics.Clear(Color.Red); graphics.FillRectangle(Brushes.Blue, 32, 0, 32, 32); }
                image.SetPixel(0, 0, Color.Transparent); image.SetPixel(31, 31, Color.Lime);
                int before = blank.Scene.TileGroups.Count;
                int imported = SceneTileImageImport.AppendImage(renderer, image);
                byte[] encoded = blank.Scene.TileGroups[imported].Pixels;
                MapEditorVerification.Require(encoded[0] == 0x1f && encoded[1] == 0x7c &&
                    encoded[2046] == 0xe0 && encoded[2047] == 3, "Imported TIL bytes must store top-left transparency first and bottom-right green last.");
                MapEditorVerification.Require(imported == before && blank.Document.Map.TileGroupCount == before + 1, "Image import did not append one group.");
                using (var first = renderer.GetTileThumbnailClone(imported, 0))
                using (var second = renderer.GetTileThumbnailClone(imported, 1))
                {
                    MapEditorVerification.Require(first.GetPixel(0, 0).A == 0 && first.GetPixel(31, 31).G == 255, "Image import changed alpha/orientation.");
                    MapEditorVerification.Require(second.GetPixel(0, 0).B == 255, "Image import did not split tile-major pixels.");
                }
                uint word = (uint)imported << 16 | 0x01000001U;
                var tiles = new SceneTileEditService(blank.Scene, blank.Scene.Layers[0]);
                tiles.SetCell(1, 1, word); tiles.SetCell(2, 2, (uint)imported << 16);
                var collision = new ConfigLayerEditService(blank.Document.Map.ConfigLayers[0]);
                collision.PaintBrush(5, 5, 5, 0, true);
                if (renderer.AvailableObjectNames.Any())
                    using (var objectForm = new SceneObjectEditorForm(renderer))
                    {
                        objectForm.Size = new Size(1000, 700); objectForm.PrepareVerificationView();
                        objectForm.VerifyAddObject();
                        objectForm.VerifyObjectDrag();
                        MapEditorVerification.SaveControl(objectForm, Path.Combine(output, "blank-object-placement.png"));
                    }
                string path = Path.Combine(output, "blank-created.md3");
                new MapBundleExportService().Export(blank.Document, path, blank.Scene, blank.IncludeEffects);
                var reopened = new MapDocumentService().Load(path);
                var scene = SceneDocument.Load(reopened);
                MapEditorVerification.Require(scene.Width == 256 && scene.Height == 256 && scene.Layers[0].GetTile(1, 1) == word,
                    "New blank map did not preserve authored dimensions or tiles.");
                MapEditorVerification.Require(reopened.Map.ConfigLayers[0].Data.Count(v => v == 0) == 21, "New map collision brush differs.");
                MapEditorVerification.Require(!File.Exists(Path.ChangeExtension(path, ".eff")), "New map inherited template effects.");
                long aggregate = new[] { path, reopened.BacPath, reopened.LyrPath, reopened.TilPath }.Sum(file => new FileInfo(file).Length);
                MapEditorVerification.Require(reopened.Map.MapSizeSum == aggregate, "New map aggregate size differs from exported bundle.");
                MapEditorVerification.Require(File.ReadAllBytes(source.BacPath).SequenceEqual(File.ReadAllBytes(reopened.BacPath)), "Template BAC was changed.");
                using (var picture = new Bitmap(512, 512))
                using (var graphics = Graphics.FromImage(picture))
                { renderer.Render(graphics, new Rectangle(0, 0, 512, 512), 2, 0, 0); picture.Save(Path.Combine(output, "blank-authored.png")); }
            }
        }
    }
}
