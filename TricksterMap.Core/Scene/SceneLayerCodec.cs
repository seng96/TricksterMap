using System;
using System.Collections.Generic;
using System.IO;

namespace TricksterMap.Scene
{
    internal static class SceneLayerCodec
    {
        internal static List<SceneLayer> Read(byte[] data, int count, int version, out byte[] prefix)
        {
            if (count < 0 || count > 4096) throw new InvalidDataException("Unsupported scene layer count.");
            int table = checked(count * 4);
            SceneBinary.Require(data, 0, table, "layer table");
            int first = count == 0 ? data.Length : checked(table + SceneBinary.Int(data, 0));
            SceneBinary.Require(data, table, first - table, "layer prefix");
            prefix = SceneBinary.Slice(data, table, first - table);
            var layers = new List<SceneLayer>();
            for (int index = 0; index < count; index++)
            {
                int start = checked(table + SceneBinary.Int(data, index * 4));
                int end = index + 1 < count ? checked(table + SceneBinary.Int(data, (index + 1) * 4)) : data.Length;
                if (start < first || end < start) throw new InvalidDataException("Scene layer offsets are not ordered.");
                SceneBinary.Require(data, start, end - (long)start, "layer block");
                SceneBinary.Require(data, start, 72, "layer header");
                if (start + 72L > end) throw new InvalidDataException("Scene layer overlaps its next block.");
                var layer = new SceneLayer {
                    NameBytes = SceneBinary.Slice(data, start, 32), Kind = SceneBinary.Int(data, start + 32),
                    Order = SceneBinary.Int(data, start + 36), ScaleX = SceneBinary.Int(data, start + 40),
                    ScaleY = SceneBinary.Int(data, start + 44), X = SceneBinary.Int(data, start + 48),
                    Y = SceneBinary.Int(data, start + 52), Width = SceneBinary.Int(data, start + 60),
                    Height = SceneBinary.Int(data, start + 64)
                };
                int length = SceneBinary.Int(data, start + 56);
                if (layer.Kind < 0 || layer.Kind > 2 || length < 0 || layer.Width < 0 || layer.Height < 0 || start + 72L + length > end)
                    throw new InvalidDataException("Scene layer dimensions or payload are invalid.");
                if (layer.Kind == 0 && length != 0) throw new InvalidDataException("Empty scene layers cannot contain pixel payload.");
                if (layer.Kind == 2 && (long)layer.Width * layer.Height * 4 != length)
                    throw new InvalidDataException("Scene tile grid dimensions do not match payload.");
                layer.Payload = SceneBinary.Slice(data, start + 68, length);
                int at = checked(start + 68 + length), objects = SceneBinary.Int(data, at), stride = version == 301 ? 48 : 56;
                at += 4;
                if (objects < 0 || at + (long)objects * stride > end)
                    throw new InvalidDataException("Scene object count exceeds layer block.");
                for (int i = 0; i < objects; i++, at += stride)
                    layer.Objects.Add(new SceneObject(SceneBinary.Slice(data, at, stride)));
                layer.TrailingData = SceneBinary.Slice(data, at, end - at);
                layers.Add(layer);
            }
            return layers;
        }

        internal static byte[] Write(IList<SceneLayer> layers, int version, byte[] prefix)
        {
            if (layers == null || layers.Count > 4096 || prefix == null) throw new InvalidDataException("Scene layers are invalid.");
            long length = layers.Count * 4L + prefix.Length;
            foreach (var layer in layers)
            {
                if (layer == null || layer.Payload == null || layer.TrailingData == null || layer.Width < 0 || layer.Height < 0 ||
                    layer.Kind < 0 || layer.Kind > 2 || (layer.Kind == 0 && layer.Payload.Length != 0))
                    throw new InvalidDataException("Unsupported or invalid scene layer metadata.");
                length += 72L + layer.Payload.Length + (long)layer.Objects.Count * (version == 301 ? 48 : 56) + layer.TrailingData.Length;
            }
            if (length > SceneBinary.Limit) throw new InvalidDataException("Scene resource exceeds supported size.");
            using (var stream = new MemoryStream((int)length))
            using (var writer = new BinaryWriter(stream))
            {
                for (int i = 0; i < layers.Count; i++) writer.Write(0);
                writer.Write(prefix);
                var offsets = new int[layers.Count];
                for (int i = 0; i < layers.Count; i++)
                {
                    var layer = layers[i];
                    if (layer == null || layer.Payload == null || layer.TrailingData == null || layer.Width < 0 || layer.Height < 0)
                        throw new InvalidDataException("Scene layer has invalid data.");
                    if (layer.Kind == 2 && (long)layer.Width * layer.Height * 4 != layer.Payload.Length)
                        throw new InvalidDataException("Scene tile grid dimensions do not match payload.");
                    offsets[i] = checked((int)stream.Position - layers.Count * 4);
                    SceneBinary.NameBytes(writer, layer.NameBytes);
                    writer.Write(layer.Kind); writer.Write(layer.Order); writer.Write(layer.ScaleX); writer.Write(layer.ScaleY);
                    writer.Write(layer.X); writer.Write(layer.Y); writer.Write(layer.Payload.Length);
                    writer.Write(layer.Width); writer.Write(layer.Height); writer.Write(layer.Payload);
                    writer.Write(layer.Objects.Count);
                    foreach (var item in layer.Objects)
                    {
                        if (item == null || item.RawData.Length != (version == 301 ? 48 : 56))
                            throw new InvalidDataException("Scene object stride does not match MDN version.");
                        writer.Write(item.RawData);
                    }
                    writer.Write(layer.TrailingData);
                    if (stream.Length > SceneBinary.Limit) throw new InvalidDataException("Scene resource exceeds supported size.");
                }
                stream.Position = 0;
                foreach (int offset in offsets) writer.Write(offset);
                return stream.ToArray();
            }
        }
    }
}
