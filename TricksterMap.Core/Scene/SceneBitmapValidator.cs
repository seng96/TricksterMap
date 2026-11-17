using System;
using System.IO;

namespace TricksterMap.Scene
{
    internal static class SceneBitmapValidator
    {
        internal static void Validate(byte[] bytes, int width, int height, bool rle)
        {
            if (bytes.Length == 0) return;
            if (width <= 0 || height <= 0 || (long)width * height > 50000000)
                throw new InvalidDataException("Scene bitmap dimensions exceed supported limits.");
            if (!rle)
            {
                if ((long)width * height * 2 != bytes.Length)
                    throw new InvalidDataException("Scene RGB555 bitmap dimensions do not match payload.");
                return;
            }
            int at = 0;
            for (int row = 0; row < height; row++)
            {
                SceneBinary.Require(bytes, at, 2, "bitmap row");
                int length = BitConverter.ToUInt16(bytes, at), end = at + length;
                if (length < 2 || end > bytes.Length) throw new InvalidDataException("Scene RLE row length is invalid.");
                int x = 0; at += 2;
                while (at + 4 <= end)
                {
                    int skip = BitConverter.ToUInt16(bytes, at), count = BitConverter.ToUInt16(bytes, at + 2); at += 4;
                    if (count == 0) break;
                    x += skip;
                    if ((long)x + count > width || at + (long)count * 2 > end)
                        throw new InvalidDataException("Scene RLE span exceeds bitmap bounds.");
                    x += count; at += count * 2;
                }
                at = end;
            }
            if (bytes.Length - at > 3) throw new InvalidDataException("Scene RLE bitmap has unsupported trailing data.");
            while (at < bytes.Length) if (bytes[at++] != 0) throw new InvalidDataException("Scene RLE alignment bytes must be zero.");
        }
    }
}
