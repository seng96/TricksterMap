using System;
using System.Collections.Generic;
using System.IO;

namespace TricksterMap.Scene
{
    /// <summary>Legacy zero-offset stream layout; accepted only when every record reaches exact EOF.</summary>
    public static class NoriSequentialAnimations
    {
        public static Dictionary<string, int[]> Read(byte[] raw, int start, int count, int version, int bitmaps, out Dictionary<string, byte[]> names)
        {
            names = new Dictionary<string, byte[]>(); var framesByName = new Dictionary<string, int[]>(); var duplicate = new HashSet<string>();
            int trailer = version == 300 ? 224 : version == 301 ? 228 : version == 302 ? 348 : version == 303 ? 352 : -1;
            if (trailer < 0 || count < 1 || count > 10000) throw new InvalidDataException("Unsupported legacy NORI record version/count.");
            for (int i = 0; i < count; i++) if (Int(raw, checked(start + i * 4)) != 0) throw new InvalidDataException("Legacy animation table must contain only zero offsets.");
            int cursor = checked(start + count * 4);
            for (int index = 0; index < count; index++)
            {
                Require(raw, cursor, 36); var name = new byte[32]; Array.Copy(raw, cursor, name, 0, 32);
                int length = Array.IndexOf(name, (byte)0); if (length < 0) length = 32;
                string key = Convert.ToBase64String(name, 0, length); int countFrames = Int(raw, cursor + 32);
                if (countFrames < 0 || countFrames > 100000) throw new InvalidDataException("Invalid legacy frame count.");
                cursor = checked(cursor + 36);
                for (int frame = 0; frame < countFrames; frame++) if (Int(raw, checked(cursor + frame * 4)) != 0) throw new InvalidDataException("Legacy frame table must contain only zero offsets.");
                cursor = checked(cursor + countFrames * 4); var positions = new int[countFrames];
                for (int frame = 0; frame < countFrames; frame++)
                {
                    positions[frame] = cursor; int planes = Int(raw, checked(cursor + 4));
                    if (planes < 0 || planes > 1024) throw new InvalidDataException("Invalid legacy plane count.");
                    Require(raw, cursor, checked(8 + planes * 28 + trailer));
                    for (int plane = 0; plane < planes; plane++)
                        if (Int(raw, cursor + 8 + plane * 28) >= bitmaps) throw new InvalidDataException("Legacy bitmap index exceeds its table.");
                    cursor = checked(cursor + 8 + planes * 28 + trailer);
                }
                if (countFrames == 0) continue;
                if (framesByName.ContainsKey(key)) duplicate.Add(key); else { framesByName.Add(key, positions); names.Add(key, name); }
            }
            if (cursor != raw.Length) throw new InvalidDataException("Legacy animation chain must reach exact file boundary.");
            foreach (string key in duplicate) { framesByName.Remove(key); names.Remove(key); }
            if (framesByName.Count == 0) throw new InvalidDataException("No unique legacy animations.");
            return framesByName;
        }
        private static int Int(byte[] raw, int at) { Require(raw, at, 4); return BitConverter.ToInt32(raw, at); }
        private static void Require(byte[] raw, int at, int length)
        { if (at < 0 || length < 0 || (long)at + length > raw.Length) throw new InvalidDataException("Truncated legacy NORI metadata."); }
    }
}
