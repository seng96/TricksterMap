using System;
using System.IO;
using System.Text;
using TricksterMap.Data;

namespace TricksterMap
{
    public class MapDataLoader
    {
        public static MapDataInfo Load(BinaryReader reader)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            if (!reader.BaseStream.CanSeek)
                throw new InvalidDataException("MDN input must support seeking.");
            long length = reader.BaseStream.Length - reader.BaseStream.Position;
            if (length > MapBinaryGuard.MaxDocumentBytes)
                throw new InvalidDataException("MDN exceeds the supported document size.");
            MapBinaryGuard.Require(reader, 96, "header");
            if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "MDN_")
                throw new InvalidDataException(Strings.InvalidMapFileError);
            var map = new MapDataInfo();
            map.Version = reader.ReadInt32();
            if (map.Version < 301 || map.Version > 303)
                throw new InvalidDataException("Unsupported MDN version: " + map.Version);
            map.TileBpp = reader.ReadInt32();
            map.Param3 = reader.ReadInt32();
            map.Param4 = reader.ReadInt32();
            if ((map.Param4 & 1) != 0)
                throw new InvalidDataException("MDN embedded resource layout is not supported (Param4 bit 0).");
            map.MapSizeSum = reader.ReadInt32();
            map.Param6 = reader.ReadInt32();
            map.Param7 = reader.ReadInt32();
            map.MapSizeX = reader.ReadInt32();
            map.MapSizeY = reader.ReadInt32();
            map.TileSizeX = reader.ReadInt32();
            map.TileSizeY = reader.ReadInt32();
            map.TileCountX = reader.ReadInt32();
            map.TileCountY = reader.ReadInt32();
            map.Param14 = reader.ReadInt32();
            map.LyrDataCount = reader.ReadInt32();
            map.TileGroupCount = reader.ReadInt32();
            int layers = reader.ReadInt32(), ranges = reader.ReadInt32();
            int points = reader.ReadInt32(), effects = reader.ReadInt32();
            map.Param21 = reader.ReadInt32();
            map.Param22 = reader.ReadInt32();
            map.Param23 = reader.ReadInt32();
            MapBinaryGuard.Count(reader, layers, 20, "configuration layers");
            MapBinaryGuard.Count(reader, ranges, 32, "ranges");
            MapBinaryGuard.Count(reader, points, 24, "points");
            MapBinaryGuard.Count(reader, effects, 52, "effects");
            MapBinaryGuard.Require(reader, (long)layers * 20 + (long)ranges * 32 +
                (long)points * 24 + (long)effects * 52, "section table");
            for (int i = 0; i < layers; i++)
            {
                MapBinaryGuard.Require(reader, 20, "configuration layer header");
                var layer = new ConfigLayer {
                    Type = reader.ReadInt32(), X = reader.ReadInt32(), Y = reader.ReadInt32(),
                    BppX = reader.ReadInt32(), BppY = reader.ReadInt32()
                };
                layer.Data = MapBinaryGuard.Read(reader,
                    MapBinaryGuard.LayerLength(layer.X, layer.Y), "configuration layer data");
                map.ConfigLayers.Add(layer);
            }
            ReadObjects(reader, map, ranges, points, effects);
            map.TrailingData = MapBinaryGuard.Read(reader,
                checked((int)(reader.BaseStream.Length - reader.BaseStream.Position)), "trailing data");
            map.OriginalSerializedLength = length;
            return map;
        }

        private static void ReadObjects(BinaryReader reader, MapDataInfo map,
            int ranges, int points, int effects)
        {
            MapBinaryGuard.Require(reader, (long)ranges * 32 + (long)points * 24 +
                (long)effects * 52, "objects");
            for (int i = 0; i < ranges; i++)
                map.RangeObjects.Add(new RangeObject {
                    Type = reader.ReadInt32(), Id = reader.ReadInt32(),
                    Destination = reader.ReadInt32(), X1 = reader.ReadInt32(), Y1 = reader.ReadInt32(),
                    X2 = reader.ReadInt32(), Y2 = reader.ReadInt32(), Options = reader.ReadInt32()
                });
            for (int i = 0; i < points; i++)
                map.PointObjects.Add(new PointObject {
                    Id = reader.ReadInt32(), Type = reader.ReadInt32(), MapId = reader.ReadInt32(),
                    X = reader.ReadInt32(), Y = reader.ReadInt32(), Options = reader.ReadInt32()
                });
            for (int i = 0; i < effects; i++)
                map.EffectObjects.Add(MapBinaryGuard.Read(reader, 52, "effect"));
        }
    }
}
