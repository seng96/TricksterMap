using System;
using System.IO;

namespace TricksterMap.Scene
{
    /// <summary>TIL pixels are tile-major, with rows top to bottom inside each tile.
    /// This is raw texture storage, not a positive-height BMP/DIB.</summary>
    public static class SceneTilePixelCodec
    {
        public static byte[] Decode(byte[] data, int offset, int width, int height, byte flags = 0)
        {
            if (width <= 0 || height <= 0 || (long)width * height > 16000000)
                throw new ArgumentOutOfRangeException("Tile dimensions exceed pixel limits.");
            SceneBinary.Require(data, offset, (long)width * height * 2, "tile pixels");
            var output = new byte[checked(width * height * 4)];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int sx = (flags & 1) != 0 ? width - 1 - x : x;
                    int sy = (flags & 2) != 0 ? height - 1 - y : y;
                    int source = offset + (sy * width + sx) * 2, target = (y * width + x) * 4;
                    int word = data[source] | data[source + 1] << 8;
                    output[target] = (byte)((word & 31) * 255 / 31);
                    output[target + 1] = (byte)(((word >> 5) & 31) * 255 / 31);
                    output[target + 2] = (byte)(((word >> 10) & 31) * 255 / 31);
                    output[target + 3] = (byte)((word & 0x7fff) == 0x7c1f ? 0 : 255);
                }
            return output;
        }

        public static byte[] EncodeAtlas(byte[] bgra, int width, int height, int tileWidth, int tileHeight)
        {
            if (width <= 0 || height <= 0 || width > 4096 || height > 4096 || tileWidth <= 0 || tileHeight <= 0 ||
                width % tileWidth != 0 || height % tileHeight != 0)
                throw new ArgumentException("Atlas dimensions must be positive tile multiples, at most 4096 pixels per side.");
            if ((long)(width / tileWidth) * (height / tileHeight) > 65535 || bgra == null || bgra.LongLength != (long)width * height * 4)
                throw new ArgumentException("Invalid atlas pixel buffer or tile count.");
            var result = new byte[checked(width * height * 2)]; int output = 0;
            for (int gy = 0; gy < height; gy += tileHeight)
                for (int gx = 0; gx < width; gx += tileWidth)
                    for (int y = 0; y < tileHeight; y++)
                        for (int x = 0; x < tileWidth; x++)
                        {
                            int at = ((gy + y) * width + gx + x) * 4;
                            int word = bgra[at + 3] < 128 ? 0x7c1f :
                                To5(bgra[at]) | To5(bgra[at + 1]) << 5 | To5(bgra[at + 2]) << 10;
                            result[output++] = (byte)word; result[output++] = (byte)(word >> 8);
                        }
            return result;
        }
        private static int To5(byte value) => (value * 31 + 127) / 255;
    }
}
