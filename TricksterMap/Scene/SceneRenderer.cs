using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using TricksterMap.Scene;
using TricksterMap.Effects;

namespace TricksterMap.SceneView
{
    /// <summary>Viewport renderer. Original scene data remains in the shared Core model.</summary>
    public sealed partial class SceneRenderer : IMapSceneRenderer, IDisposable
    {
        private readonly SceneImageCache cache = new SceneImageCache();
        private readonly Dictionary<SceneLayer, SceneBitmapSource> bitmaps = new Dictionary<SceneLayer, SceneBitmapSource>();
        private readonly HashSet<string> warnings = new HashSet<string>();
        private readonly Dictionary<int, ImageAttributes> opacityAttributes = new Dictionary<int, ImageAttributes>();
        private NoriSceneResource objects;
        private MapEffectPreview effects;
        private readonly bool hasAdditivePlanes;
        private bool disposed;
        public SceneDocument Scene { get; }
        public IEnumerable<string> Warnings => warnings.Concat(effects == null ? Enumerable.Empty<string>() : effects.Warnings).Distinct().OrderBy(value => value).ToArray();
        public double AnimationTimeSeconds { get; set; }
        public bool AnimateObjects { get; set; }
        public bool HasAnimatedObjects => objects != null && objects.HasAnimation || effects != null && effects.HasEffects;
        public bool ShowEffects { get; set; } = true;
        public string ResourceRoot { get; private set; }
        public EffectDependencyReport EffectDependencies => effects?.Dependencies;
        public int EffectParticleCount => effects?.ParticleCount ?? 0;
        public void LoadEffectResources(string root = null)
        { ResourceRoot = root; effects = new MapEffectPreview(Scene.Document, cache, root); }
        public IEnumerable<SceneObjectAsset> AvailableObjectNames => objects == null ?
            Enumerable.Empty<SceneObjectAsset>() : objects.Assets.ToArray();

        public static SceneRenderer Load(MapDocument document) { return new SceneRenderer(SceneDocument.Load(document)); }

        /// <summary>Caller owns the returned image; the scene cache remains private.</summary>
        public Bitmap GetTileThumbnailClone(int group, int tile, byte flags = 0)
        {
            if (disposed) throw new ObjectDisposedException(nameof(SceneRenderer));
            if (group < 0 || group >= Scene.TileGroups.Count || tile < 0 || tile == 65535 ||
                tile >= (long)Scene.TileGroups[group].Width * Scene.TileGroups[group].Height)
                throw new ArgumentOutOfRangeException(nameof(tile));
            var image = cache.Get("tile:" + group + ":" + tile + ":" + (flags & 0x23), () =>
                ScenePixels.Tile(Scene.TileGroups[group].Pixels, checked(tile * Scene.TileWidth * Scene.TileHeight * 2),
                    Scene.TileWidth, Scene.TileHeight, flags));
            return image.Clone(new Rectangle(0, 0, image.Width, image.Height), PixelFormat.Format32bppArgb);
        }

        public SceneRenderer(SceneDocument scene)
        {
            Scene = scene ?? throw new ArgumentNullException(nameof(scene));
            if (scene.Width <= 0 || scene.Height <= 0 || scene.Width > 100000 || scene.Height > 100000)
                throw new InvalidDataException("Scene map dimensions are outside preview limits.");
            warnings.Add("精細預覽：場景深度遮擋尚未與遊戲逐像素驗收。");
            PrepareBitmaps();
            if (File.Exists(scene.Document.BacPath) || scene.Layers.Any(layer => layer.Objects.Count > 0))
            {
                try
                {
                    byte[] raw = SceneEnvelope.Unpack(File.ReadAllBytes(scene.Document.BacPath), 0xa0b0);
                    objects = new NoriSceneResource(raw, cache);
                    hasAdditivePlanes = objects.HasAdditivePlanes;
                    foreach (string warning in objects.Warnings) warnings.Add(warning);
                    int missing = scene.Layers.SelectMany(layer => layer.Objects).Count(obj => !objects.Contains(obj.NameBytes));
                    if (missing > 0) warnings.Add("BAC 無對應或名稱重複的場景物件：" + missing + " 筆。");
                    if (HasAnimatedObjects) warnings.Add("場景動畫可選播放；時序未與遊戲內事件同步。");
                }
                catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is ArgumentException || ex is OverflowException)
                { warnings.Add("BAC 場景物件未載入：" + ex.Message); }
            }
            LoadEffectResources();
        }

        private void PrepareBitmaps()
        {
            bitmaps.Clear();
            foreach (var layer in Scene.Layers)
            {
                if (layer.ScaleX != 100 || layer.ScaleY != 100)
                    warnings.Add("非預設圖層捲動比例尚未模擬。");
                if (layer.Kind == 1 && layer.Payload.Length > 0)
                    bitmaps[layer] = new SceneBitmapSource(layer.Payload, layer.Width, layer.Height, Scene.Document.Map.Param3 == 1);
                if (layer.Kind == 2)
                    for (int at = 0; at + 4 <= layer.Payload.Length; at += 4)
                    {
                        uint word = BitConverter.ToUInt32(layer.Payload, at);
                        if ((word & 0xffff) == 0xffff) continue;
                        uint flags = word >> 24;
                        if ((flags & 0x10) != 0) warnings.Add("動態頂點地磚以靜態位置預覽。");
                        if ((flags & ~0x33U) != 0) warnings.Add("部分地磚含尚未模擬的旗標。");
                        if ((flags & 0x20) != 0) warnings.Add("半透明地磚已套用一般分支顏色；完整光照狀態未還原。");
                    }
            }
        }

        /// <summary>Call after changing pixel payloads; tile-cell references are read live.</summary>
        public void Invalidate()
        {
            if (disposed) return;
            cache.Dispose(); PrepareBitmaps();
        }

        public void Render(Graphics graphics, Rectangle viewport, double zoom, double originX, double originY)
            => Render(graphics, viewport, zoom, originX, originY, null);

        internal void Render(Graphics graphics, Rectangle viewport, double zoom, double originX, double originY,
            SceneObjectMovePreview move)
        {
            if (disposed) throw new ObjectDisposedException(nameof(SceneRenderer));
            if (graphics == null) throw new ArgumentNullException(nameof(graphics));
            if (double.IsNaN(zoom) || double.IsInfinity(zoom) || zoom <= 0 || zoom > 64 ||
                double.IsNaN(originX) || double.IsInfinity(originX) || double.IsNaN(originY) || double.IsInfinity(originY))
                throw new ArgumentOutOfRangeException(nameof(zoom));
            if (viewport.Width <= 0 || viewport.Height <= 0) return;
            bool drawEffects = ShowEffects && effects != null && effects.HasEffects;
            bool shadows = objects != null && Scene.Layers.Any(layer => layer.Order == 5 && layer.Objects.Count > 0);
            if (drawEffects || shadows || hasAdditivePlanes)
            {
                RenderBuffered(graphics, viewport, zoom, originX, originY, move, drawEffects);
                return;
            }
            RenderBase(graphics, viewport, zoom, originX, originY, move);
        }

        private void RenderBase(Graphics graphics, Rectangle viewport, double zoom, double originX, double originY,
            SceneObjectMovePreview move, Bitmap surface = null)
        {
            GraphicsState state = graphics.Save();
            try
            {
                graphics.SetClip(viewport); graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                graphics.PixelOffsetMode = PixelOffsetMode.Half; graphics.CompositingMode = CompositingMode.SourceOver;
                graphics.FillRectangle(Brushes.DarkSlateGray, viewport);
                for (int index = 0; index < Scene.Layers.Count; index++)
                {
                    var layer = Scene.Layers[index];
                    double left = originX + layer.X * zoom, top = originY + layer.Y * zoom;
                    try
                    {
                        if (layer.Kind == 1) PaintBitmap(graphics, viewport, layer, index, zoom, left, top);
                        else if (layer.Kind == 2) PaintTiles(graphics, viewport, layer, zoom, left, top);
                        PaintObjects(graphics, viewport, layer, zoom, left, top, move, surface);
                    }
                    catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is ArgumentException || ex is OverflowException || ex is ExternalException)
                    { warnings.Add("圖層 " + index + " 預覽未完成：" + ex.Message); }
                }
            }
            finally { graphics.Restore(state); }
        }

        private void PaintBitmap(Graphics graphics, Rectangle view, SceneLayer layer, int index, double zoom, double left, double top)
        {
            SceneBitmapSource source;
            if (!bitmaps.TryGetValue(layer, out source)) return;
            const int chunk = 256;
            int x0 = Math.Max(0, (int)Math.Floor((view.Left - left) / zoom / chunk));
            int y0 = Math.Max(0, (int)Math.Floor((view.Top - top) / zoom / chunk));
            int x1 = Math.Min((layer.Width + chunk - 1) / chunk, (int)Math.Ceiling((view.Right - left) / zoom / chunk));
            int y1 = Math.Min((layer.Height + chunk - 1) / chunk, (int)Math.Ceiling((view.Bottom - top) / zoom / chunk));
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                {
                    var region = new Rectangle(x * chunk, y * chunk, Math.Min(chunk, layer.Width - x * chunk), Math.Min(chunk, layer.Height - y * chunk));
                    var image = cache.Get("layer:" + index + ":" + x + ":" + y, () => source.Decode(region));
                    graphics.DrawImage(image, Destination(left + region.X * zoom, top + region.Y * zoom, region.Width * zoom, region.Height * zoom));
                }
        }

        private void PaintTiles(Graphics graphics, Rectangle view, SceneLayer layer, double zoom, double left, double top)
        {
            double tileWidth = Scene.TileWidth * zoom, tileHeight = Scene.TileHeight * zoom;
            int x0 = Math.Max(0, (int)Math.Floor((view.Left - left) / tileWidth));
            int y0 = Math.Max(0, (int)Math.Floor((view.Top - top) / tileHeight));
            int x1 = Math.Min(layer.Width, (int)Math.Ceiling((view.Right - left) / tileWidth));
            int y1 = Math.Min(layer.Height, (int)Math.Ceiling((view.Bottom - top) / tileHeight));
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                {
                    uint word = layer.GetTile(x, y); int tile = (int)(word & 65535), group = (int)((word >> 16) & 255);
                    if (tile == 65535) continue;
                    if (group >= Scene.TileGroups.Count || tile >= (long)Scene.TileGroups[group].Width * Scene.TileGroups[group].Height)
                    { warnings.Add("部分地磚索引超出素材範圍。"); continue; }
                    byte flags = (byte)(word >> 24);
                    var pixels = Scene.TileGroups[group].Pixels;
                    var image = cache.Get("tile:" + group + ":" + tile + ":" + (flags & 0x23), () =>
                        ScenePixels.Tile(pixels, checked(tile * Scene.TileWidth * Scene.TileHeight * 2), Scene.TileWidth, Scene.TileHeight, flags));
                    graphics.DrawImage(image, Destination(left + x * tileWidth, top + y * tileHeight, tileWidth, tileHeight));
                }
        }

        private void PaintObjects(Graphics graphics, Rectangle view, SceneLayer layer, double zoom, double left, double top,
            SceneObjectMovePreview move, Bitmap surface)
        {
            if (objects == null) return;
            foreach (var obj in OrderedObjects(layer, move))
            {
                bool moving = move?.Item == obj;
                double x = moving ? move.X : obj.X, y = moving ? move.Y : obj.Y;
                foreach (var sprite in CurrentSprites(obj))
                {
                    if (sprite.Opacity == 0) continue;
                    var destination = Destination(left + (x + sprite.X) * zoom,
                        top + (y - obj.Z + sprite.Y) * zoom, sprite.Width * zoom, sprite.Height * zoom);
                    if (!view.IntersectsWith(destination) || destination.Width <= 0 || destination.Height <= 0) continue;
                    string blend = sprite.Blend == NoriSceneSprite.AdditiveBlend ? "ADD" :
                        layer.Order == 5 && sprite.Blend == 6 ? "SCENE_SHADOW" : null;
                    if (blend != null && surface != null)
                        CompositePlane(graphics, surface, view, destination, sprite, blend);
                    else
                        DrawPlane(graphics, destination, sprite);
                }
            }
        }

        private ImageAttributes Opacity(int percent)
        {
            ImageAttributes result;
            if (!opacityAttributes.TryGetValue(percent, out result))
            {
                result = new ImageAttributes(); var matrix = new ColorMatrix { Matrix33 = percent / 100f };
                result.SetColorMatrix(matrix); opacityAttributes.Add(percent, result);
            }
            return result;
        }

        private static Rectangle Destination(double x, double y, double width, double height)
        {
            int left = (int)Math.Round(x), top = (int)Math.Round(y);
            return Rectangle.FromLTRB(left, top, (int)Math.Round(x + width), (int)Math.Round(y + height));
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true; cache.Dispose();
            foreach (var attributes in opacityAttributes.Values) attributes.Dispose();
            opacityAttributes.Clear(); bitmaps.Clear(); objects = null; effects = null;
        }
    }
}
