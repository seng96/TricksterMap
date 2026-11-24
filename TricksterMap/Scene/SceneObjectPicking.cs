using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using TricksterMap.Scene;

namespace TricksterMap.SceneView
{
    internal sealed class SceneObjectMovePreview
    {
        public SceneObject Item { get; }
        public int X { get; }
        public int Y { get; }
        public SceneObjectMovePreview(SceneObject item, int x, int y)
        { Item = item; X = x; Y = y; }
    }

    public sealed partial class SceneRenderer
    {
        private static IEnumerable<SceneObject> OrderedObjects(SceneLayer layer, SceneObjectMovePreview move = null)
            => layer.Objects.OrderBy(item => (long)(move?.Item == item ? move.Y : item.Y) - item.Z);

        private IEnumerable<NoriSceneSprite> CurrentSprites(SceneObject item)
        {
            double seconds = double.IsNaN(AnimationTimeSeconds) || double.IsInfinity(AnimationTimeSeconds) ? 0 : AnimationTimeSeconds;
            return objects.Sprites(item.NameBytes, seconds, AnimateObjects);
        }

        internal SceneObject PickObject(SceneLayer layer, double worldX, double worldY)
        {
            if (disposed || objects == null || layer == null) return null;
            foreach (var item in OrderedObjects(layer).Reverse())
            {
                try
                {
                    foreach (var sprite in CurrentSprites(item).Reverse())
                    {
                        if (sprite.Opacity == 0) continue;
                        double x = worldX - layer.X - item.X - sprite.X;
                        double y = worldY - layer.Y - item.Y + item.Z - sprite.Y;
                        if (x < 0 || y < 0 || x >= sprite.Width || y >= sprite.Height) continue;
                        int sx = (sprite.Flip & 1) == 0 ? (int)x : sprite.Width - 1 - (int)x;
                        int sy = (sprite.Flip & 2) == 0 ? (int)y : sprite.Height - 1 - (int)y;
                        if (sprite.Image.GetPixel(sx, sy).A > 0) return item;
                    }
                }
                catch (Exception ex) when (ex is IOException || ex is InvalidDataException ||
                    ex is ArgumentException || ex is OverflowException || ex is ExternalException)
                { warnings.Add("部分場景物件無法選取：" + ex.Message); }
            }
            return null;
        }
    }
}
