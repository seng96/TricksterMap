using System;
using System.IO;

namespace TricksterMap.Scene
{
    /// <summary>Appends tile-major RGB555 pixels; image decoding belongs to the UI layer.</summary>
    public sealed class SceneTileImportService
    {
        public int Append(SceneDocument scene, int tilesX, int tilesY, byte[] tileMajorRgb555)
        {
            if (scene == null) throw new ArgumentNullException(nameof(scene));
            scene.ValidateHeaderCounts();
            if (scene.TileBpp != 16 || scene.TileWidth != 32 || scene.TileHeight != 32)
                throw new InvalidDataException("RGB555 import requires 32x32 16-bit scene tiles.");
            if (tilesX <= 0 || tilesY <= 0 || (long)tilesX * tilesY > 65535)
                throw new ArgumentOutOfRangeException("A tile group must contain 1..65535 tiles.");
            if (scene.TileGroups.Count >= 256) throw new InvalidOperationException("Tile group IDs are limited to 0..255.");
            long length = (long)tilesX * tilesY * 2048;
            if (tileMajorRgb555 == null || tileMajorRgb555.LongLength != length)
                throw new ArgumentException("Tile-major RGB555 byte length does not match group dimensions.");
            long total = scene.TilePrefixLength + length + (scene.TileGroups.Count + 1L) * 16;
            foreach (var group in scene.TileGroups)
            {
                if (group == null || group.Pixels == null || group.TrailingData == null || group.Width <= 0 || group.Height <= 0 ||
                    (long)group.Width * group.Height * 2048 != group.Pixels.Length)
                    throw new InvalidDataException("Existing scene tile group is invalid.");
                total += group.Pixels.Length + (long)group.TrailingData.Length;
            }
            if (total > SceneBinary.Limit) throw new InvalidDataException("Imported tiles would exceed the scene resource size limit.");
            // Finish every validation and independent allocation before mutation.
            var appended = new SceneTileGroup { Width = tilesX, Height = tilesY, Pixels = (byte[])tileMajorRgb555.Clone() };
            int index = scene.TileGroups.Count;
            scene.TileGroups.Add(appended);
            scene.Document.Map.TileGroupCount = scene.TileGroups.Count;
            return index;
        }
    }
}
