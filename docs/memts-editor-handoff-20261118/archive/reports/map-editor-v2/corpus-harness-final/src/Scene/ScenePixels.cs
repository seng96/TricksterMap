using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace TricksterMap.SceneView
{
    internal static class ScenePixels
    {
        internal static void Require(byte[] data, int at, int count)
        {
            if (at < 0 || count < 0 || (long)at + count > data.Length)
                throw new InvalidDataException("Scene pixel data is truncated.");
        }

        internal static int Int(byte[] data, int at) { Require(data, at, 4); return BitConverter.ToInt32(data, at); }
        internal static ushort Word(byte[] data, int at) { Require(data, at, 2); return BitConverter.ToUInt16(data, at); }

        internal static Bitmap FromBgra(byte[] pixels, int width, int height)
        {
            var image = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            var locked = image.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                for (int row = 0; row < height; row++)
                    Marshal.Copy(pixels, row * width * 4, IntPtr.Add(locked.Scan0, row * locked.Stride), width * 4);
            }
            finally { image.UnlockBits(locked); }
            return image;
        }

        internal static void Rgb555(byte[] data, int at, byte[] target, int output)
        {
            ushort pixel = Word(data, at);
            int blue = pixel & 31, green = (pixel >> 5) & 31, red = (pixel >> 10) & 31;
            // Match the existing corpus reader's BGR;15 normalized channels.
            target[output] = (byte)(blue * 255 / 31);
            target[output + 1] = (byte)(green * 255 / 31);
            target[output + 2] = (byte)(red * 255 / 31);
            target[output + 3] = (byte)((pixel & 0x7fff) == 0x7c1f ? 0 : 255);
        }

        internal static Bitmap Tile(byte[] data, int at, int width, int height, byte flags)
        {
            Require(data, at, checked(width * height * 2));
            var output = new byte[checked(width * height * 4)];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int sourceX = (flags & 1) != 0 ? width - 1 - x : x;
                    int sourceY = (flags & 2) != 0 ? y : height - 1 - y;
                    int target = (y * width + x) * 4;
                    Rgb555(data, at + (sourceY * width + sourceX) * 2, output, target);
                    if ((flags & 0x20) != 0) output[target + 3] = (byte)(output[target + 3] * 160 / 255);
                }
            return FromBgra(output, width, height);
        }
    }
}
