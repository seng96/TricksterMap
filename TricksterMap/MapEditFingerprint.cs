using System;
using System.IO;
using System.Security.Cryptography;
using TricksterMap.Data;
using TricksterMap.Scene;

namespace TricksterMap
{
    /// <summary>Tracks editable model values for discard prompts; not an MDN serializer.</summary>
    internal static class MapEditFingerprint
    {
        public static string ComputeScene(SceneDocument scene)
        {
            using (var hash = SHA256.Create())
            using (var crypto = new CryptoStream(Stream.Null, hash, CryptoStreamMode.Write))
            using (var writer = new BinaryWriter(crypto))
            {
                byte[] layers = scene.SerializeLayers(), tiles = scene.SerializeTiles();
                writer.Write(layers.Length); writer.Write(layers);
                writer.Write(tiles.Length); writer.Write(tiles); writer.Flush();
                crypto.FlushFinalBlock();
                return Convert.ToBase64String(hash.Hash);
            }
        }

        public static string Compute(MapDataInfo map)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            using (var hash = SHA256.Create())
            {
                foreach (var layer in map.ConfigLayers) { writer.Write(layer.Data.Length); writer.Write(layer.Data); }
                writer.Write(map.PointObjects.Count);
                foreach (var p in map.PointObjects)
                { writer.Write(p.Id); writer.Write(p.Type); writer.Write(p.MapId); writer.Write(p.X); writer.Write(p.Y); writer.Write(p.Options); }
                writer.Write(map.RangeObjects.Count);
                foreach (var r in map.RangeObjects)
                { writer.Write(r.Type); writer.Write(r.Id); writer.Write(r.Destination); writer.Write(r.X1); writer.Write(r.Y1); writer.Write(r.X2); writer.Write(r.Y2); writer.Write(r.Options); }
                foreach (var effect in map.EffectObjects) { writer.Write(effect.Length); writer.Write(effect); }
                writer.Flush(); stream.Position = 0;
                return Convert.ToBase64String(hash.ComputeHash(stream));
            }
        }
    }
}
