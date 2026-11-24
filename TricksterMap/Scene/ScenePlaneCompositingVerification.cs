using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace TricksterMap.SceneView
{
    internal static class ScenePlaneCompositingVerification
    {
        internal static void Run()
        {
            VerifyShadow();
            VerifyAdditive();
        }

        private static void VerifyShadow()
        {
            var light = Color.FromArgb(200, 150, 100);
            using (var destination = new Bitmap(3, 1, PixelFormat.Format32bppArgb))
            using (var mask = new Bitmap(3, 1, PixelFormat.Format32bppArgb))
            {
                mask.SetPixel(0, 0, Color.White);
                mask.SetPixel(1, 0, Color.FromArgb(128, 170, 204));
                mask.SetPixel(2, 0, Color.Transparent);
                using (var graphics = Graphics.FromImage(destination))
                {
                    graphics.Clear(light);
                    graphics.Flush(FlushIntention.Sync);
                    EffectBlend.Apply(destination, mask, new Rectangle(0, 0, 3, 1), "SCENE_SHADOW");
                }
                MapEditorVerification.Require(destination.GetPixel(0, 0).ToArgb() == light.ToArgb() &&
                    destination.GetPixel(2, 0).ToArgb() == light.ToArgb(), "White or transparent shadows changed the ground.");
                MapEditorVerification.Require(destination.GetPixel(1, 0).ToArgb() == Color.FromArgb(100, 100, 80).ToArgb(),
                    "Shadow pixels did not multiply the underlying color.");
            }
        }

        private static void VerifyAdditive()
        {
            var ground = Color.FromArgb(200, 150, 100);
            using (var destination = new Bitmap(4, 1, PixelFormat.Format32bppArgb))
            using (var plane = new Bitmap(4, 1, PixelFormat.Format32bppArgb))
            {
                for (int x = 0; x < 4; x++) destination.SetPixel(x, 0, ground);
                plane.SetPixel(0, 0, Color.Black);
                plane.SetPixel(1, 0, Color.FromArgb(0, 255, 0, 255));
                plane.SetPixel(2, 0, Color.FromArgb(64, 128, 24));
                plane.SetPixel(3, 0, Color.FromArgb(128, 60, 40, 20));
                EffectBlend.Apply(destination, plane, new Rectangle(0, 0, 4, 1), "ADD");
                MapEditorVerification.Require(destination.GetPixel(0, 0).ToArgb() == ground.ToArgb() &&
                    destination.GetPixel(1, 0).ToArgb() == ground.ToArgb(), "Black or transparent light obscured the scene.");
                MapEditorVerification.Require(destination.GetPixel(2, 0).ToArgb() == Color.FromArgb(255, 255, 124).ToArgb() &&
                    destination.GetPixel(3, 0).ToArgb() == Color.FromArgb(230, 170, 110).ToArgb(), "Additive light did not clamp or scale by opacity.");
            }
        }
    }
}
