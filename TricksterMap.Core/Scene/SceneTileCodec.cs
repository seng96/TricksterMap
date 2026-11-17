using System;
using System.Collections.Generic;
using System.IO;

namespace TricksterMap.Scene
{
    internal static class SceneTileCodec
    {
        internal static List<SceneTileGroup> Read(byte[] data, int count, int bytesPerTile, out byte[] prefix)
        {
            if (count < 0 || count > 256 || bytesPerTile <= 0) throw new InvalidDataException("Invalid tile group metadata.");
            int table = checked(count * 4);
            SceneBinary.Require(data, 0, table, "tile group table");
            int first = count == 0 ? data.Length : checked(table + SceneBinary.Int(data, 0));
            SceneBinary.Require(data, table, first - table, "tile prefix");
            prefix = SceneBinary.Slice(data, table, first - table);
            var groups = new List<SceneTileGroup>();
            for (int index = 0; index < count; index++)
            {
                int start = checked(table + SceneBinary.Int(data, index * 4));
                int end = index + 1 < count ? checked(table + SceneBinary.Int(data, (index + 1) * 4)) : data.Length;
                if (start < first || end < start) throw new InvalidDataException("Tile group offsets are not ordered.");
                SceneBinary.Require(data, start, end - (long)start, "tile group");
                if (start + 12L > end) throw new InvalidDataException("Tile group header is incomplete.");
                int length = SceneBinary.Int(data, start), width = SceneBinary.Int(data, start + 4), height = SceneBinary.Int(data, start + 8);
                if (width <= 0 || height <= 0 || (long)width * height > 100000 ||
                    (long)width * height * bytesPerTile != length || start + 12L + length > end)
                    throw new InvalidDataException("Tile group dimensions or payload length are invalid.");
                groups.Add(new SceneTileGroup { Width = width, Height = height,
                    Pixels = SceneBinary.Slice(data, start + 12, length),
                    TrailingData = SceneBinary.Slice(data, start + 12 + length, end - start - 12 - length) });
            }
            return groups;
        }

        internal static byte[] Write(IList<SceneTileGroup> groups, int bytesPerTile, byte[] prefix)
        {
            if (groups == null || groups.Count > 256 || prefix == null) throw new InvalidDataException("Invalid tile group metadata.");
            long length = groups.Count * 4L + prefix.Length;
            foreach (var group in groups)
            {
                if (group == null || group.Pixels == null || group.TrailingData == null)
                    throw new InvalidDataException("Invalid tile group data.");
                length += 12L + group.Pixels.Length + group.TrailingData.Length;
            }
            if (length > SceneBinary.Limit) throw new InvalidDataException("Scene resource exceeds supported size.");
            using (var stream = new MemoryStream((int)length))
            using (var writer = new BinaryWriter(stream))
            {
                for (int i = 0; i < groups.Count; i++) writer.Write(0);
                writer.Write(prefix);
                var offsets = new int[groups.Count];
                for (int i = 0; i < groups.Count; i++)
                {
                    var group = groups[i];
                    if (group == null || group.Width <= 0 || group.Height <= 0 || group.Pixels == null || group.TrailingData == null ||
                        (long)group.Width * group.Height > 100000 ||
                        (long)group.Width * group.Height * bytesPerTile != group.Pixels.Length)
                        throw new InvalidDataException("Tile group dimensions or payload length are invalid.");
                    offsets[i] = checked((int)stream.Position - groups.Count * 4);
                    writer.Write(group.Pixels.Length); writer.Write(group.Width); writer.Write(group.Height);
                    writer.Write(group.Pixels); writer.Write(group.TrailingData);
                    if (stream.Length > SceneBinary.Limit) throw new InvalidDataException("Scene resource exceeds supported size.");
                }
                stream.Position = 0; foreach (int offset in offsets) writer.Write(offset);
                return stream.ToArray();
            }
        }
    }
}
