using System;
using System.Drawing;
using System.IO;

namespace TricksterMap.SceneView
{
    internal sealed class NoriScenePixels
    {
        private readonly byte[] raw, palette;
        private readonly int[] offsets;
        private readonly int bits, compression;
        internal NoriScenePixels(byte[] raw, int[] offsets, int bits, int compression, byte[] palette)
        { this.raw = raw; this.offsets = offsets; this.bits = bits; this.compression = compression; this.palette = palette; }

        internal Bitmap Decode(int index)
        {
            if (index < 0 || index >= offsets.Length) throw new InvalidDataException("BAC bitmap index exceeds its table.");
            int at = offsets[index], size = ScenePixels.Int(raw, at + 4), width = ScenePixels.Int(raw, at + 8), height = ScenePixels.Int(raw, at + 12);
            if (ScenePixels.Int(raw, at) != 1 || width <= 0 || height <= 0 || width > 8192 || height > 8192 ||
                (long)width * height > 16000000 || size < 0) throw new InvalidDataException("Invalid BAC bitmap header.");
            int unit = bits / 8, stride = checked(width * unit), pos = checked(at + 28);
            ScenePixels.Require(raw, pos, size);
            var output = new byte[checked(width * height * 4)];
            if (compression == 0)
            {
                if (size < (long)stride * height) throw new InvalidDataException("BAC raw bitmap is truncated.");
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++) Pixel(pos + y * stride + x * unit, output, (y * width + x) * 4);
            }
            else
            {
                int maximum = checked(pos + size);
                for (int y = 0; y < height; y++)
                {
                    int length = ScenePixels.Word(raw, pos), end = checked(pos + length), cursor = 0; pos += 2;
                    if (length < 2 || end > maximum) throw new InvalidDataException("BAC RLE row length is invalid.");
                    while (pos + 4 <= end)
                    {
                        int skip = ScenePixels.Word(raw, pos), count = ScenePixels.Word(raw, pos + 2); pos += 4;
                        if (count == 0) break;
                        cursor += skip;
                        if (cursor + count > width || (long)pos + count * unit > end) throw new InvalidDataException("BAC RLE run exceeds its row.");
                        for (int x = 0; x < count; x++) Pixel(pos + x * unit, output, (y * width + cursor + x) * 4);
                        cursor += count; pos += count * unit;
                    }
                    pos = end;
                }
            }
            return ScenePixels.FromBgra(output, width, height);
        }

        private void Pixel(int at, byte[] output, int target)
        {
            if (bits == 16) { ScenePixels.Rgb555(raw, at, output, target); return; }
            int red, green, blue;
            if (bits == 8)
            {
                int index = raw[at];
                if (palette == null || index * 3 + 2 >= palette.Length) throw new InvalidDataException("BAC palette index is missing.");
                red = palette[index * 3]; green = palette[index * 3 + 1]; blue = palette[index * 3 + 2];
                if (index == 0) return;
            }
            else { blue = raw[at]; green = raw[at + 1]; red = raw[at + 2]; }
            output[target] = (byte)blue; output[target + 1] = (byte)green; output[target + 2] = (byte)red;
            output[target + 3] = (byte)(red == 255 && green == 0 && blue == 255 ? 0 : 255);
        }
    }
}
