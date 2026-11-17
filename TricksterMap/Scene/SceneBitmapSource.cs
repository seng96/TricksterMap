using System;
using System.Drawing;
using System.IO;

namespace TricksterMap.SceneView
{
    /// <summary>Decodes only a visible region, including bottom-up bitmap RLE.</summary>
    internal sealed class SceneBitmapSource
    {
        private readonly byte[] data;
        private readonly int width, height;
        private readonly int[] rows;

        internal SceneBitmapSource(byte[] data, int width, int height, bool rle)
        {
            if (width <= 0 || height <= 0 || (long)width * height > 50000000)
                throw new InvalidDataException("Scene bitmap dimensions are invalid.");
            this.data = data; this.width = width; this.height = height;
            if (!rle) { ScenePixels.Require(data, 0, checked(width * height * 2)); return; }
            rows = new int[height]; int at = 0;
            for (int row = 0; row < height; row++)
            {
                rows[row] = at;
                int length = ScenePixels.Word(data, at);
                if (length < 2) throw new InvalidDataException("Scene bitmap RLE row is invalid.");
                ScenePixels.Require(data, at, length); at += length;
            }
            if (data.Length - at > 3) throw new InvalidDataException("Unknown bitmap RLE trailing data.");
        }

        internal Bitmap Decode(Rectangle region)
        {
            if (region.Width <= 0 || region.Height <= 0 || region.Left < 0 || region.Top < 0 ||
                region.Right > width || region.Bottom > height) throw new ArgumentOutOfRangeException(nameof(region));
            var output = new byte[checked(region.Width * region.Height * 4)];
            for (int y = 0; y < region.Height; y++)
            {
                int sourceY = height - 1 - (region.Top + y);
                if (rows == null)
                {
                    for (int x = 0; x < region.Width; x++)
                        ScenePixels.Rgb555(data, (sourceY * width + region.Left + x) * 2, output, (y * region.Width + x) * 4);
                }
                else DecodeRleRow(sourceY, region.Left, region.Width, output, y * region.Width * 4);
            }
            return ScenePixels.FromBgra(output, region.Width, region.Height);
        }

        private void DecodeRleRow(int row, int left, int count, byte[] output, int outputAt)
        {
            int at = rows[row], end = at + ScenePixels.Word(data, at), cursor = 0; at += 2;
            while (at + 4 <= end)
            {
                int skip = ScenePixels.Word(data, at), length = ScenePixels.Word(data, at + 2); at += 4;
                if (length == 0) break;
                cursor += skip;
                if (cursor + length > width || (long)at + length * 2 > end)
                    throw new InvalidDataException("Scene bitmap RLE run exceeds its row.");
                int start = Math.Max(left, cursor), stop = Math.Min(left + count, cursor + length);
                for (int x = start; x < stop; x++)
                    ScenePixels.Rgb555(data, at + (x - cursor) * 2, output, outputAt + (x - left) * 4);
                cursor += length; at += length * 2;
            }
        }
    }
}
