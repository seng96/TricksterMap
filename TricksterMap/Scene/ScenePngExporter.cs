using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace TricksterMap.SceneView
{
    internal static class ScenePngExporter
    {
        internal static void Export(SceneRenderer renderer, string path)
        {
            if (renderer == null) throw new ArgumentNullException(nameof(renderer));
            string output = Path.GetFullPath(path);
            if (!string.Equals(Path.GetExtension(output), ".png", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("輸出檔名必須使用 .png 副檔名。", nameof(path));
            string temporary = Path.Combine(Path.GetDirectoryName(output), ".map-png-" + Guid.NewGuid().ToString("N"));
            int width = renderer.Scene.Width, height = renderer.Scene.Height;
            if ((long)width * height * 4 > int.MaxValue)
                throw new InvalidOperationException("地圖尺寸超過單張 PNG 點陣圖可處理的範圍。");
            try
            {
                using (var image = new Bitmap(width, height, PixelFormat.Format32bppArgb))
                {
                    using (var graphics = Graphics.FromImage(image))
                    {
                        // EFF renders to a bounded buffer. Render in world-aligned bands so large maps retain effects.
                        int rows = Math.Min(height, Math.Max(1, 4194304 / width));
                        for (int y = 0; y < height; y += rows)
                            renderer.Render(graphics, new Rectangle(0, y, width, Math.Min(rows, height - y)), 1, 0, 0);
                    }
                    using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write))
                        image.Save(stream, ImageFormat.Png);
                }
                if (File.Exists(output)) File.Replace(temporary, output, null);
                else File.Move(temporary, output);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
