using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using TricksterMap.Data;

namespace TricksterMap
{
    internal static class MapEditorTabs
    {
        public static List<MapCollisionEditorForm> Populate(TabControl tabs, MapDocument document)
        {
            var editors = new List<MapCollisionEditorForm>();
            var map = document.Map;
            foreach (var layer in map.ConfigLayers)
                if (layer.Type == 1)
                {
                    var editor = new MapCollisionEditorForm(layer, editors.Count == 0 ? map : null);
                    Add(tabs, "Collision " + layer.X + " x " + layer.Y, editor);
                    editors.Add(editor);
                }
            var points = new PointObjectForm { Map = map };
            points.RepopulateData(); Add(tabs, "Point objects", points);
            var ranges = new RangeObjectForm { Map = map };
            ranges.RepopulateData(); Add(tabs, "Range objects", ranges);
            if (editors.Count > 0)
            {
                var viewport = editors[0];
                viewport.MapObjectsEdited += (s, e) =>
                {
                    points.RepopulateData();
                    ranges.RepopulateData();
                };
                viewport.MapObjectEditRequested += item =>
                {
                    if (item is PointObject point)
                    {
                        int index = map.PointObjects.IndexOf(point);
                        if (points.EditAt(index, tabs.FindForm()))
                            viewport.RecordMapObjectDialogEdit(point, map.PointObjects[index]);
                    }
                    else if (item is RangeObject range)
                    {
                        int index = map.RangeObjects.IndexOf(range);
                        if (ranges.EditAt(index, tabs.FindForm()))
                            viewport.RecordMapObjectDialogEdit(range, map.RangeObjects[index]);
                    }
                };
            }
            return editors;
        }

        private static void Add(TabControl tabs, string title, Form form)
        {
            var page = new TabPage(title);
            form.TopLevel = false; form.FormBorderStyle = FormBorderStyle.None; form.Dock = DockStyle.Fill;
            page.Controls.Add(form); tabs.TabPages.Add(page); form.Show();
        }

        private static void AddTiles(TabControl tabs, MapDocument document)
        {
            List<TileData> data;
            using (var stream = File.OpenRead(document.TilPath)) data = TileReader.Read(document.Map, stream);
            var atlases = new List<Bitmap>();
            try
            {
                foreach (var group in data)
                {
                    var bitmap = new Bitmap(checked(group.TilesX * document.Map.TileSizeX), checked(group.TilesY * document.Map.TileSizeY));
                    atlases.Add(bitmap);
                    using (var graphics = Graphics.FromImage(bitmap))
                        for (int i = 0; i < group.Bitmaps.Count; i++)
                            graphics.DrawImageUnscaled(group.Bitmaps[i], (i % group.TilesX) * document.Map.TileSizeX,
                                (i / group.TilesX) * document.Map.TileSizeY);
                }
                var viewer = new TileViewForm { Map = document.Map, tiles = atlases };
                viewer.FormClosed += (s, e) => { foreach (var atlas in atlases) atlas.Dispose(); };
                viewer.Populate(); Add(tabs, "Tile atlas (legacy preview)", viewer);
            }
            catch { foreach (var atlas in atlases) atlas.Dispose(); throw; }
            finally { foreach (var group in data) foreach (var bitmap in group.Bitmaps) bitmap.Dispose(); }
        }
    }
}
