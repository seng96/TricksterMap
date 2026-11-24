using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace TricksterMap.SceneView
{
    /// <summary>Opaque scene-target blend equations; alpha-test GPU state remains a preview limitation.</summary>
    internal static class EffectBlend
    {
        internal static void Apply(Bitmap target, Bitmap source, Rectangle destination, string mode)
        {
            var targetBits = target.LockBits(destination, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            var sourceBits = source.LockBits(new Rectangle(0, 0, source.Width, source.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                var dst = new byte[destination.Width * 4]; var src = new byte[source.Width * 4];
                for (int y = 0; y < destination.Height; y++)
                {
                    IntPtr output = IntPtr.Add(targetBits.Scan0, y * targetBits.Stride);
                    Marshal.Copy(output, dst, 0, dst.Length); Marshal.Copy(IntPtr.Add(sourceBits.Scan0, y * sourceBits.Stride), src, 0, src.Length);
                    for (int x = 0; x < dst.Length; x += 4)
                    {
                        int alpha = src[x + 3]; if (alpha == 0) continue;
                        for (int channel = 0; channel < 3; channel++)
                        {
                            int d = dst[x + channel], s = src[x + channel], value;
                            if (mode == "ADD") value = d + (s * alpha + 127) / 255;
                            else if (mode == "MULT") value = d * s / 255;
                            else if (mode == "SCENE_SHADOW") value = (d * (65025 - alpha * (255 - s)) + 32512) / 65025;
                            else if (mode == "INVMULT") value = d * (255 - s) / 255;
                            else if (mode == "INVERSE") value = s * (255 - s) / 255;
                            else value = (s * alpha + d * (255 - alpha) + 127) / 255;
                            dst[x + channel] = (byte)Math.Max(0, Math.Min(255, value));
                        }
                        dst[x + 3] = 255;
                    }
                    Marshal.Copy(dst, 0, output, dst.Length);
                }
            }
            finally { source.UnlockBits(sourceBits); target.UnlockBits(targetBits); }
        }
    }
}
