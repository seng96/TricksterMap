using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using TricksterMap.Scene;
using TricksterMap.SceneView;

namespace TricksterMap.SceneEditing
{
    /// <summary>Imports a complete tile atlas; source images remain unchanged.</summary>
    internal static class SceneTileImageImport
    {
        public static int Import(IWin32Window owner, SceneRenderer renderer)
        {
            using (var dialog = new OpenFileDialog { Filter = "地磚圖集 PNG/BMP|*.png;*.bmp|所有圖像|*.png;*.bmp;*.jpg;*.jpeg" })
            {
                if (dialog.ShowDialog(owner) != DialogResult.OK) return -1;
                try
                {
                    using (var image = Image.FromFile(dialog.FileName)) return AppendImage(renderer, image);
                }
                catch (Exception ex) when (ex is ArgumentException || ex is IOException ||
                    ex is InvalidOperationException || ex is ExternalException || ex is OverflowException)
                { MessageBox.Show(owner, ex.Message, "無法匯入地磚圖集", MessageBoxButtons.OK, MessageBoxIcon.Error); return -1; }
            }
        }

        internal static int AppendImage(SceneRenderer renderer, Image source)
        {
            if (renderer == null) throw new ArgumentNullException(nameof(renderer));
            int width, height;
            byte[] pixels = Encode(source, renderer.Scene.TileWidth, renderer.Scene.TileHeight, out width, out height);
            int index = new SceneTileImportService().Append(renderer.Scene, width, height, pixels);
            renderer.Invalidate(); return index;
        }

        internal static byte[] Encode(Image source, int tileWidth, int tileHeight, out int tilesX, out int tilesY)
        {
            if (source == null || tileWidth <= 0 || tileHeight <= 0) throw new ArgumentException("圖像或地磚尺寸無效。");
            if (source.Width > 4096 || source.Height > 4096 || source.Width % tileWidth != 0 || source.Height % tileHeight != 0)
                throw new ArgumentException("圖集尺寸須為地磚尺寸的整數倍，且每邊最多4096像素。\r\n目前地磚尺寸：" + tileWidth + " × " + tileHeight);
            tilesX = source.Width / tileWidth; tilesY = source.Height / tileHeight;
            if ((long)tilesX * tilesY > 65535) throw new ArgumentException("單一圖集最多65535個地磚。");
            using (var bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb))
            {
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                    graphics.DrawImageUnscaled(source, 0, 0);
                }
                byte[] rgba = new byte[checked(bitmap.Width * bitmap.Height * 4)];
                var locked = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                    ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                try
                {
                    int rowLength = bitmap.Width * 4;
                    for (int y = 0; y < bitmap.Height; y++)
                        Marshal.Copy(IntPtr.Add(locked.Scan0, y * locked.Stride), rgba, y * rowLength, rowLength);
                }
                finally { bitmap.UnlockBits(locked); }
                return SceneTilePixelCodec.EncodeAtlas(rgba, bitmap.Width, bitmap.Height, tileWidth, tileHeight);
            }
        }

    }
}
