using System;
using System.IO;
using TricksterMap.Data;

namespace TricksterMap
{
    internal static class MapSerializer
    {
        internal static byte[] Serialize(MapDataInfo map)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (map.Version < 301 || map.Version > 303)
                throw new InvalidDataException("Unsupported MDN version: " + map.Version);
            if ((map.Param4 & 1) != 0)
                throw new InvalidDataException("MDN embedded resource layout is not supported (Param4 bit 0).");
            long length = GetLength(map);
            long sum = map.OriginalSerializedLength.HasValue
                ? (long)map.MapSizeSum + length - map.OriginalSerializedLength.Value
                : (map.MapSizeSum != 0 ? map.MapSizeSum : length + map.BacFileSize + map.TilFileSize + map.LyrFileSize);
            if (sum < int.MinValue || sum > int.MaxValue)
                throw new InvalidDataException("MDN aggregate size overflow.");
            using (var stream = new MemoryStream((int)length))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(new byte[] { 77, 68, 78, 95 });
                int[] header = { map.Version, map.TileBpp, map.Param3, map.Param4, (int)sum,
                    map.Param6, map.Param7, map.MapSizeX, map.MapSizeY, map.TileSizeX, map.TileSizeY,
                    map.TileCountX, map.TileCountY, map.Param14, map.LyrDataCount, map.TileGroupCount,
                    map.ConfigLayers.Count, map.RangeObjects.Count, map.PointObjects.Count,
                    map.EffectObjects.Count, map.Param21, map.Param22, map.Param23 };
                foreach (int value in header) writer.Write(value);
                WriteLayers(writer, map);
                WriteObjects(writer, map);
                writer.Write(map.TrailingData);
                return stream.ToArray();
            }
        }

        private static long GetLength(MapDataInfo map)
        {
            if (map.ConfigLayers == null || map.RangeObjects == null || map.PointObjects == null ||
                map.EffectObjects == null || map.TrailingData == null)
                throw new InvalidDataException("MDN sections cannot be null.");
            long length = 96L + 32L * map.RangeObjects.Count + 24L * map.PointObjects.Count +
                52L * map.EffectObjects.Count + map.TrailingData.Length;
            foreach (var layer in map.ConfigLayers)
            {
                if (layer == null || layer.Data == null ||
                    layer.Data.Length != MapBinaryGuard.LayerLength(layer.X, layer.Y))
                    throw new InvalidDataException("MDN configuration layer length mismatch.");
                length += 20L + layer.Data.Length;
            }
            foreach (var effect in map.EffectObjects)
                if (effect == null || effect.Length != 52)
                    throw new InvalidDataException("MDN effects must contain exactly 52 bytes.");
            foreach (var point in map.PointObjects)
                if (point == null) throw new InvalidDataException("MDN point cannot be null.");
            foreach (var range in map.RangeObjects)
                if (range == null) throw new InvalidDataException("MDN range cannot be null.");
            if (length > MapBinaryGuard.MaxDocumentBytes)
                throw new InvalidDataException("MDN exceeds the supported document size.");
            return length;
        }

        private static void WriteLayers(BinaryWriter writer, MapDataInfo map)
        {
            foreach (var layer in map.ConfigLayers)
            {
                writer.Write(layer.Type); writer.Write(layer.X); writer.Write(layer.Y);
                writer.Write(layer.BppX); writer.Write(layer.BppY); writer.Write(layer.Data);
            }
        }

        private static void WriteObjects(BinaryWriter writer, MapDataInfo map)
        {
            foreach (var range in map.RangeObjects)
            {
                writer.Write(range.Type); writer.Write(range.Id); writer.Write(range.Destination);
                writer.Write(range.X1); writer.Write(range.Y1); writer.Write(range.X2);
                writer.Write(range.Y2); writer.Write(range.Options);
            }
            foreach (var point in map.PointObjects)
            {
                writer.Write(point.Id); writer.Write(point.Type); writer.Write(point.MapId);
                writer.Write(point.X); writer.Write(point.Y); writer.Write(point.Options);
            }
            foreach (var effect in map.EffectObjects) writer.Write(effect);
        }
    }
}
