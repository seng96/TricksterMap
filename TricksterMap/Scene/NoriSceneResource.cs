using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using TricksterMap.Scene;

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
        private readonly string cachePrefix = "nori:" + Guid.NewGuid().ToString("N") + ":";
        internal readonly List<string> Warnings = new List<string>();
        internal readonly List<SceneObjectAsset> Assets = new List<SceneObjectAsset>();
        internal bool HasAnimation => animations.Values.Any(frames => frames.Length > 1);

        internal NoriSceneResource(byte[] raw, SceneImageCache cache) : this(new NoriAnimationIndex(raw), cache) { }
        internal NoriSceneResource(NoriAnimationIndex index, SceneImageCache cache)
        {
            raw = index.RawData; this.cache = cache; bitmaps = index.BitmapOffsets; animations = index.Animations;
            pixels = new NoriScenePixels(raw, bitmaps, index.Bits, index.Compression, index.Palette);
            Warnings.AddRange(index.Warnings);
            foreach (byte[] name in index.Names) Assets.Add(new SceneObjectAsset(name));
        }
        internal static string NameKey(byte[] name) => NoriAnimationIndex.NameKey(name);

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
            foreach (var sprite in ReadFrame(frames[frame])) yield return sprite;
        }

        internal double[] Delays(byte[] name)
        {
            int[] frames;
            return animations.TryGetValue(NameKey(name), out frames) ? frames.Select(at => Math.Max(0, ScenePixels.Int(raw, at)) * 0.0010000000474974513).ToArray() : new[] { .1 };
        }

        internal IEnumerable<NoriSceneSprite> SpritesAtFrame(byte[] name, int frame)
        {
            int[] frames;
            if (!animations.TryGetValue(NameKey(name), out frames)) yield break;
            foreach (var sprite in ReadFrame(frames[Math.Max(0, Math.Min(frames.Length - 1, frame))])) yield return sprite;
        }

        private IEnumerable<NoriSceneSprite> ReadFrame(int at)
        {
            int planes = ScenePixels.Int(raw, at + 4);
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

        internal Bitmap GetBitmap(int index) { return cache.Get(cachePrefix + index, () => pixels.Decode(index)); }
    }

    internal sealed class NoriSceneSprite
    {
        internal NoriSceneResource Resource;
        internal Bitmap Image => Resource.GetBitmap(BitmapIndex);
        internal int BitmapIndex, X, Y, Width, Height, Opacity, Flip;
    }
}
