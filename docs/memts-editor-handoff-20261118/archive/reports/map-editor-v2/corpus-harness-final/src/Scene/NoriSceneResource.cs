using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace TricksterMap.SceneView
{
    /// <summary>Read-only NORI/GAWI view. Names are matched by bytes, not replacement text.</summary>
    internal sealed class NoriSceneResource
    {
        private readonly byte[] raw;
        private readonly int[] bitmaps;
        private readonly Dictionary<string, int[]> animations;
        private readonly SceneImageCache cache;
        private readonly NoriScenePixels pixels;
        internal readonly List<string> Warnings = new List<string>();
        internal readonly List<SceneObjectAsset> Assets = new List<SceneObjectAsset>();
        internal bool HasAnimation => animations.Values.Any(frames => frames.Length > 1);

        internal NoriSceneResource(byte[] raw, SceneImageCache cache)
        {
            this.raw = raw; this.cache = cache;
            if (raw.Length < 84 || ScenePixels.Int(raw, 0) != 0x49524f4e || ScenePixels.Int(raw, 40) != 0x49574147)
                throw new InvalidDataException("BAC requires NORI/GAWI data.");
            int bits = ScenePixels.Int(raw, 48), compression = ScenePixels.Int(raw, 52);
            if (!new[] { 8, 16, 24, 32 }.Contains(bits) || compression < 0 || compression > 1)
                throw new InvalidDataException("Unsupported BAC pixel format.");
            int count = ScenePixels.Int(raw, 76), at = 84;
            if (count < 0 || count > 100000) throw new InvalidDataException("BAC bitmap count exceeds limits.");
            byte[] palette = null;
            if (ScenePixels.Int(raw, 56) != 0)
            {
                if (ScenePixels.Int(raw, at) != 0x5f4c4150) throw new InvalidDataException("Invalid NORI palette.");
                int length = ScenePixels.Int(raw, at + 28);
                if (length < 40) throw new InvalidDataException("Invalid NORI palette length.");
                ScenePixels.Require(raw, at, length);
                palette = new byte[length - 40]; Array.Copy(raw, at + 32, palette, 0, palette.Length);
                at += length;
            }
            int bitmapBase = checked(at + count * 4);
            ScenePixels.Require(raw, at, count * 4);
            bitmaps = new int[count];
            for (int i = 0; i < count; i++) bitmaps[i] = checked(bitmapBase + ScenePixels.Int(raw, at + i * 4) + (compression == 1 ? 28 * i : 0));
            int animationCount = ScenePixels.Int(raw, 28), allocation = ScenePixels.Int(raw, 32), total = ScenePixels.Int(raw, 36);
            if (animationCount < 0 || animationCount > 10000) throw new InvalidDataException("Invalid NORI animation count.");
            int animationStart = checked(total - allocation + 40);
            if (total == raw.Length) animations = ReadAnimations(animationStart, animationCount, false);
            else
            {
                var valid = new List<Tuple<int, Dictionary<string, int[]>>>();
                foreach (int candidate in new[] { animationStart, checked(raw.Length - allocation + 40), checked(40 + ScenePixels.Int(raw, 80)) }.Distinct())
                {
                    if (total > raw.Length && candidate == animationStart) continue;
                    try { valid.Add(Tuple.Create(candidate, ReadAnimations(candidate, animationCount, true))); }
                    catch (Exception ex) when (ex is InvalidDataException || ex is OverflowException || ex is ArgumentException) { }
                }
                if (valid.Count != 1) throw new InvalidDataException("BAC stale length does not identify a unique animation table.");
                animationStart = valid[0].Item1; animations = valid[0].Item2;
                Warnings.Add("BAC 舊長度欄位不一致，採用已驗證的唯一動畫表；原檔不變。");
            }
            if (compression == 0 && !bitmaps.All(offset => ValidRawBitmap(offset, bitmapBase, animationStart, bits)))
            {
                var recovered = new int[count]; int cursor = bitmapBase;
                for (int i = 0; i < count; i++)
                {
                    if (!ValidRawBitmap(cursor, bitmapBase, animationStart, bits)) throw new InvalidDataException("BAC bitmap offsets are invalid.");
                    recovered[i] = cursor; cursor = checked(cursor + 28 + ScenePixels.Int(raw, cursor + 4));
                }
                if (cursor != animationStart) throw new InvalidDataException("BAC bitmap recovery does not reach the animation table.");
                Array.Copy(recovered, bitmaps, count); Warnings.Add("BAC 圖片位移依完整區塊鏈驗證恢復，原檔不變。");
            }
            pixels = new NoriScenePixels(raw, bitmaps, bits, compression, palette);
            var named = new HashSet<string>();
            for (int i = 0; i < animationCount; i++)
            {
                int nameAt = checked(animationStart + animationCount * 4 + ScenePixels.Int(raw, animationStart + i * 4));
                var name = new byte[32]; Array.Copy(raw, nameAt, name, 0, 32); string key = NameKey(name);
                if (animations.ContainsKey(key) && named.Add(key)) Assets.Add(new SceneObjectAsset(name));
            }
        }

        private bool ValidRawBitmap(int at, int minimum, int maximum, int bits)
        {
            try
            {
                int size = ScenePixels.Int(raw, at + 4), width = ScenePixels.Int(raw, at + 8), height = ScenePixels.Int(raw, at + 12);
                return at >= minimum && ScenePixels.Int(raw, at) == 1 && width > 0 && height > 0 && width <= 8192 && height <= 8192 &&
                    (long)width * height <= 16000000 && (long)width * height * (bits / 8) <= size && (long)at + 28 + size <= maximum;
            }
            catch (InvalidDataException) { return false; }
        }

        private Dictionary<string, int[]> ReadAnimations(int start, int count, bool validate)
        {
            if (start < 84) throw new InvalidDataException("BAC animation table is invalid.");
            ScenePixels.Require(raw, start, checked(count * 4));
            var result = new Dictionary<string, int[]>();
            var duplicates = new HashSet<string>(); int previous = -1;
            for (int i = 0; i < count; i++)
            {
                int at = checked(start + count * 4 + ScenePixels.Int(raw, start + i * 4));
                if (at < start + count * 4 || (validate && at <= previous)) throw new InvalidDataException("BAC animation offsets are invalid.");
                previous = at; ScenePixels.Require(raw, at, 36);
                byte[] name = new byte[32]; Array.Copy(raw, at, name, 0, 32);
                string key = NameKey(name); int frames = ScenePixels.Int(raw, at + 32), table = at + 36;
                if (frames < 0 || frames > 100000) throw new InvalidDataException("BAC frame count exceeds limits.");
                ScenePixels.Require(raw, table, checked(frames * 4));
                var positions = new int[frames]; int priorFrame = -1;
                for (int frame = 0; frame < frames; frame++)
                {
                    int position = checked(table + frames * 4 + ScenePixels.Int(raw, table + frame * 4));
                    ScenePixels.Require(raw, position, 8);
                    if (position < table + frames * 4 || (validate && position <= priorFrame)) throw new InvalidDataException("BAC frame offsets are invalid.");
                    int planes = ScenePixels.Int(raw, position + 4);
                    if (planes < 0 || planes > 1024) throw new InvalidDataException("BAC plane count exceeds limits.");
                    ScenePixels.Require(raw, position + 8, checked(planes * 28));
                    positions[frame] = position; priorFrame = position;
                }
                if (frames == 0) continue;
                if (result.ContainsKey(key)) duplicates.Add(key); else result.Add(key, positions);
            }
            foreach (string key in duplicates) result.Remove(key);
            if (validate && result.Count == 0) throw new InvalidDataException("No unique BAC animations validate the candidate table.");
            return result;
        }

        internal static string NameKey(byte[] name)
        {
            int length = Array.IndexOf(name, (byte)0); if (length < 0) length = name.Length;
            return Convert.ToBase64String(name, 0, length);
        }

        internal bool Contains(byte[] name) { return animations.ContainsKey(NameKey(name)); }

        internal IEnumerable<NoriSceneSprite> Sprites(byte[] name, double seconds, bool animate)
        {
            int[] frames;
            if (!animations.TryGetValue(NameKey(name), out frames)) yield break;
            int frame = 0;
            if (animate && frames.Length > 1)
            {
                long total = 0;
                foreach (int position in frames) total += Math.Max(1, ScenePixels.Int(raw, position));
                double elapsed = ((Math.Max(0, seconds) * 1000) % total);
                while (frame + 1 < frames.Length && elapsed >= Math.Max(1, ScenePixels.Int(raw, frames[frame])))
                { elapsed -= Math.Max(1, ScenePixels.Int(raw, frames[frame])); frame++; }
            }
            int at = frames[frame], planes = ScenePixels.Int(raw, at + 4);
            for (int i = 0; i < planes; i++)
            {
                int record = at + 8 + i * 28, bitmap = ScenePixels.Int(raw, record);
                if (bitmap < 0) continue;
                int opacity = ScenePixels.Int(raw, record + 12), flip = ScenePixels.Int(raw, record + 16);
                if (flip < 0 || flip > 3) throw new InvalidDataException("Unsupported BAC plane flip flags: " + flip);
                if (bitmap >= bitmaps.Length) throw new InvalidDataException("BAC bitmap index exceeds its table.");
                int width = ScenePixels.Int(raw, bitmaps[bitmap] + 8), height = ScenePixels.Int(raw, bitmaps[bitmap] + 12);
                yield return new NoriSceneSprite { Resource = this, BitmapIndex = bitmap, Width = width, Height = height, X = ScenePixels.Int(raw, record + 4),
                    Y = ScenePixels.Int(raw, record + 8), Opacity = Math.Max(0, Math.Min(100, opacity)), Flip = flip };
            }
        }

        internal Bitmap GetBitmap(int index) { return cache.Get("nori:" + index, () => pixels.Decode(index)); }
    }

    internal sealed class NoriSceneSprite
    {
        internal NoriSceneResource Resource;
        internal Bitmap Image => Resource.GetBitmap(BitmapIndex);
        internal int BitmapIndex, X, Y, Width, Height, Opacity, Flip;
    }
}
