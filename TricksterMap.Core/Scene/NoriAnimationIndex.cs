using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TricksterMap.Scene
{
    /// <summary>Shared read-only NORI metadata validation; image decoding stays in the GUI.</summary>
    public sealed class NoriAnimationIndex
    {
        private readonly byte[] raw;
        private readonly int[] bitmaps;
        private readonly Dictionary<string, int[]> animations;
        public byte[] RawData => raw;
        public int[] BitmapOffsets => bitmaps;
        public Dictionary<string, int[]> Animations => animations;
        public int Bits { get; private set; }
        public int Compression { get; private set; }
        public byte[] Palette { get; private set; }
        public List<byte[]> Names { get; } = new List<byte[]>();
        public List<string> Warnings { get; } = new List<string>();
        private static void Require(byte[] data, int at, int size) => SceneBinary.Require(data, at, size, "NORI");
        public NoriAnimationIndex(byte[] raw)
        {
            this.raw = raw;
            if (raw.Length < 84 || SceneBinary.Int(raw, 0) != 0x49524f4e || SceneBinary.Int(raw, 40) != 0x49574147)
                throw new InvalidDataException("BAC requires NORI/GAWI data.");
            int bits = SceneBinary.Int(raw, 48), compression = SceneBinary.Int(raw, 52);
            if (!new[] { 8, 16, 24, 32 }.Contains(bits) || compression < 0 || compression > 1)
                throw new InvalidDataException("Unsupported BAC pixel format.");
            int count = SceneBinary.Int(raw, 76), at = 84;
            if (count < 0 || count > 100000) throw new InvalidDataException("BAC bitmap count exceeds limits.");
            byte[] palette = null;
            if (SceneBinary.Int(raw, 56) != 0)
            {
                if (SceneBinary.Int(raw, at) != 0x5f4c4150) throw new InvalidDataException("Invalid NORI palette.");
                int length = SceneBinary.Int(raw, at + 28);
                if (length < 40) throw new InvalidDataException("Invalid NORI palette length.");
                Require(raw, at, length);
                palette = new byte[length - 40]; Array.Copy(raw, at + 32, palette, 0, palette.Length);
                at += length;
            }
            int bitmapBase = checked(at + count * 4);
            Require(raw, at, count * 4);
            bitmaps = new int[count];
            for (int i = 0; i < count; i++) bitmaps[i] = checked(bitmapBase + SceneBinary.Int(raw, at + i * 4) + (compression == 1 ? 28 * i : 0));
            int animationCount = SceneBinary.Int(raw, 28), allocation = SceneBinary.Int(raw, 32), total = SceneBinary.Int(raw, 36);
            if (animationCount < 0 || animationCount > 10000) throw new InvalidDataException("Invalid NORI animation count.");
            int animationStart = checked(total - allocation + 40);
            Dictionary<string, byte[]> sequentialNames = null;
            if (total == raw.Length) animations = ReadAnimations(animationStart, animationCount, false);
            else
            {
                var valid = new List<Tuple<int, Dictionary<string, int[]>>>();
                var candidates = new List<int> { animationStart, checked(raw.Length - allocation + 40), checked(40 + SceneBinary.Int(raw, 80)) };
                // Older raw NRI libraries may zero all length fields. Their
                // complete self-sized bitmap chain supplies a structural boundary,
                // not a scan for arbitrary animation-looking bytes. A declared
                // longer file is still treated as truncated, never relocated.
                int chainEnd = -1;
                if (compression == 0 && total <= raw.Length)
                {
                    int cursor = bitmapBase; bool chain = true;
                    for (int i = 0; i < count; i++)
                    {
                        if (!ValidRawBitmap(cursor, bitmapBase, raw.Length, bits)) { chain = false; break; }
                        cursor = checked(cursor + 28 + SceneBinary.Int(raw, cursor + 4));
                    }
                    if (chain && cursor < raw.Length) { candidates.Add(cursor); chainEnd = cursor; }
                }
                foreach (int candidate in candidates.Distinct())
                {
                    if (total > raw.Length && candidate == animationStart) continue;
                    try { valid.Add(Tuple.Create(candidate, ReadAnimations(candidate, animationCount, true))); }
                    catch (Exception ex) when (ex is InvalidDataException || ex is OverflowException || ex is ArgumentException) { }
                }
                if (valid.Count == 0 && chainEnd >= 0)
                {
                    animations = NoriSequentialAnimations.Read(raw, chainEnd, animationCount, SceneBinary.Int(raw, 4), count, out sequentialNames);
                    animationStart = chainEnd;
                    Warnings.Add("NORI 零位移表依完整版本化影格鏈驗證至檔尾；原檔不變。");
                }
                else
                {
                    if (valid.Count != 1) throw new InvalidDataException("BAC stale length does not identify a unique animation table.");
                    animationStart = valid[0].Item1; animations = valid[0].Item2;
                    Warnings.Add("BAC 舊長度欄位不一致，採用已驗證的唯一動畫表；原檔不變。");
                }
            }
            if (compression == 0 && !bitmaps.All(offset => ValidRawBitmap(offset, bitmapBase, animationStart, bits)))
            {
                var recovered = new int[count]; int cursor = bitmapBase;
                for (int i = 0; i < count; i++)
                {
                    if (!ValidRawBitmap(cursor, bitmapBase, animationStart, bits)) throw new InvalidDataException("BAC bitmap offsets are invalid.");
                    recovered[i] = cursor; cursor = checked(cursor + 28 + SceneBinary.Int(raw, cursor + 4));
                }
                if (cursor != animationStart) throw new InvalidDataException("BAC bitmap recovery does not reach the animation table.");
                Array.Copy(recovered, bitmaps, count); Warnings.Add("BAC 圖片位移依完整區塊鏈驗證恢復，原檔不變。");
            }
            Bits = bits; Compression = compression; Palette = palette;
            var named = new HashSet<string>();
            if (sequentialNames != null) foreach (var name in sequentialNames.Values) Names.Add(name);
            else
            for (int i = 0; i < animationCount; i++)
            {
                int nameAt = checked(animationStart + animationCount * 4 + SceneBinary.Int(raw, animationStart + i * 4));
                var name = new byte[32]; Array.Copy(raw, nameAt, name, 0, 32); string key = NameKey(name);
                if (animations.ContainsKey(key) && named.Add(key)) Names.Add(name);
            }
        }

        private bool ValidRawBitmap(int at, int minimum, int maximum, int bits)
        {
            try
            {
                int size = SceneBinary.Int(raw, at + 4), width = SceneBinary.Int(raw, at + 8), height = SceneBinary.Int(raw, at + 12);
                return at >= minimum && SceneBinary.Int(raw, at) == 1 && width > 0 && height > 0 && width <= 8192 && height <= 8192 &&
                    (long)width * height <= 16000000 && (long)width * height * (bits / 8) <= size && (long)at + 28 + size <= maximum;
            }
            catch (InvalidDataException) { return false; }
        }

        private Dictionary<string, int[]> ReadAnimations(int start, int count, bool validate)
        {
            if (start < 84) throw new InvalidDataException("BAC animation table is invalid.");
            Require(raw, start, checked(count * 4));
            var result = new Dictionary<string, int[]>();
            var duplicates = new HashSet<string>(); int previous = -1;
            for (int i = 0; i < count; i++)
            {
                int at = checked(start + count * 4 + SceneBinary.Int(raw, start + i * 4));
                if (at < start + count * 4 || (validate && at <= previous)) throw new InvalidDataException("BAC animation offsets are invalid.");
                previous = at; Require(raw, at, 36);
                byte[] name = new byte[32]; Array.Copy(raw, at, name, 0, 32);
                string key = NameKey(name); int frames = SceneBinary.Int(raw, at + 32), table = at + 36;
                if (frames < 0 || frames > 100000) throw new InvalidDataException("BAC frame count exceeds limits.");
                Require(raw, table, checked(frames * 4));
                var positions = new int[frames]; int priorFrame = -1;
                for (int frame = 0; frame < frames; frame++)
                {
                    int position = checked(table + frames * 4 + SceneBinary.Int(raw, table + frame * 4));
                    Require(raw, position, 8);
                    if (position < table + frames * 4 || (validate && position <= priorFrame)) throw new InvalidDataException("BAC frame offsets are invalid.");
                    int planes = SceneBinary.Int(raw, position + 4);
                    if (planes < 0 || planes > 1024) throw new InvalidDataException("BAC plane count exceeds limits.");
                    Require(raw, position + 8, checked(planes * 28));
                    positions[frame] = position; priorFrame = position;
                }
                if (frames == 0) continue;
                if (result.ContainsKey(key)) duplicates.Add(key); else result.Add(key, positions);
            }
            foreach (string key in duplicates) result.Remove(key);
            if (validate && result.Count == 0) throw new InvalidDataException("No unique BAC animations validate the candidate table.");
            return result;
        }

        public static string NameKey(byte[] name)
        {
            int length = Array.IndexOf(name, (byte)0); if (length < 0) length = name.Length;
            return Convert.ToBase64String(name, 0, length);
        }

    }
}
