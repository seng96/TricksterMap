using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using TricksterMap.Effects;
using TricksterMap.Scene;

namespace TricksterMap.SceneView
{
    internal sealed class MapEffectPreview
    {
        private readonly NoriSceneResource material;
        private readonly EffectSimulation simulation;
        private readonly Dictionary<string, byte[]> names = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        internal EffectDependencyReport Dependencies { get; }
        internal readonly HashSet<string> Warnings = new HashSet<string>();
        internal bool HasEffects => simulation != null;
        internal int ParticleCount => simulation?.ParticleCount ?? 0;
        internal MapEffectPreview(MapDocument document, SceneImageCache cache, string root)
        {
            Dependencies = EffectDependencyReport.Inspect(document, root);
            foreach (string warning in Dependencies.Warnings) Warnings.Add(warning);
            if (Dependencies.Document == null || Dependencies.MaterialIndex == null) return;
            try
            {
                material = new NoriSceneResource(Dependencies.MaterialIndex, cache);
                foreach (string warning in material.Warnings) Warnings.Add(warning);
                foreach (var cream in Dependencies.Document.Creams.Values)
                {
                    if (cream.Number("TextureKind") != 0) Warnings.Add("未支援 TextureKind，該效果不繪製：" + cream.Name);
                    if (!SupportedBlend(cream.Text("CreamBlendMode", "NORMAL"))) Warnings.Add("未支援混合模式，該效果不繪製：" + cream.Text("CreamBlendMode"));
                }
                foreach (string texture in Dependencies.Document.Creams.Values.Select(c => c.Text("TextureName")).Distinct())
                {
                    byte[] encoded = Encoding.GetEncoding(949, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback).GetBytes(texture);
                    if (encoded.Length > 32) { Warnings.Add("EFF 材質名稱超過32 bytes：" + texture); continue; }
                    var name = new byte[32]; encoded.CopyTo(name, 0); names.Add(texture, name);
                    if (!material.Contains(name)) Warnings.Add("EFF 材質沒有唯一動畫：" + texture);
                }
                var placements = new List<MapEffectInstance>();
                foreach (byte[] raw in document.Map.EffectObjects)
                    try { var item = new MapEffectInstance(raw); var unused = item.Name; placements.Add(item); }
                    catch (ArgumentException ex) { Warnings.Add("略過無效的特效實例：" + ex.Message); }
                simulation = new EffectSimulation(Dependencies.Document, placements, name => names.ContainsKey(name) ? material.Delays(names[name]) : new[] { .1 });
                if (placements.Count > 0) Warnings.Add("EFF 使用固定種子／60秒離線循環；深度排序、GPU alpha-test 與遊戲事件時序尚未逐像素驗收。");
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is ArgumentException || ex is OverflowException || ex is ExternalException)
            { Warnings.Add("EFF 材質預覽無法載入：" + ex.Message); }
        }
        internal void Render(Bitmap target, double zoom, double originX, double originY, double seconds)
        {
            if (simulation == null) return;
            simulation.Seek(seconds);
            foreach (string warning in simulation.Warnings) Warnings.Add(warning);
            foreach (var particle in simulation.Particles)
            {
                if (particle.Spec.Number("TextureKind") != 0 || !SupportedBlend(particle.Spec.Text("CreamBlendMode", "NORMAL"))) continue;
                byte[] name;
                if (!names.TryGetValue(particle.Spec.Text("TextureName"), out name)) continue;
                foreach (var sprite in material.SpritesAtFrame(name, particle.Animation.Frame))
                    try { Draw(target, sprite, particle, zoom, originX, originY); }
                    catch (Exception ex) when (ex is ArgumentException || ex is OverflowException || ex is ExternalException || ex is IOException || ex is InvalidDataException)
                    { Warnings.Add("EFF 粒子略過：" + ex.Message); }
            }
        }
        private static void Draw(Bitmap target, NoriSceneSprite sprite, EffectParticle p, double zoom, double originX, double originY)
        {
            var position = p.Linked ? p.Position + p.Anchor : p.Position;
            double x = originX + position.X * zoom, y = originY - (position.Y + position.Z) * Math.Sqrt(.5) * zoom;
            double sx = p.Size[0] * .01 * zoom, sy = p.Size[1] * .01 * zoom, theta = p.Angle * Math.PI / 180;
            double co = Math.Cos(theta), si = Math.Sin(theta), align = p.Spec.Number("IsZAlign") == 0 ? 1 : Math.Sqrt(.5);
            double a = co * sx, b = si * sy, c = -si * sx * align, d = co * sy * align;
            if (new[] { x, y, a, b, c, d, p.Alpha, p.Color.X, p.Color.Y, p.Color.Z }.Any(v => double.IsNaN(v) || double.IsInfinity(v))) return;
            if (sx == 0 || sy == 0 || p.Alpha <= 0 || sprite.Opacity == 0) return;
            Func<double, double, PointF> point = (u, v) => new PointF((float)(x + a * u + b * v), (float)(y + c * u + d * v));
            double left = sprite.X, right = left + sprite.Width, top = sprite.Y, bottom = top + sprite.Height;
            var corners = new[] { point(left, top), point(right, top), point(left, bottom), point(right, bottom) };
            if (corners.Any(q => Math.Abs(q.X) > 100000000 || Math.Abs(q.Y) > 100000000)) return;
            var area = Rectangle.Intersect(new Rectangle(0, 0, target.Width, target.Height), Rectangle.FromLTRB(
                (int)Math.Floor(corners.Min(q => q.X)), (int)Math.Floor(corners.Min(q => q.Y)), (int)Math.Ceiling(corners.Max(q => q.X)), (int)Math.Ceiling(corners.Max(q => q.Y))));
            if (area.Width <= 0 || area.Height <= 0 || (long)area.Width * area.Height > 16000000) return;
            double firstX = (sprite.Flip & 1) == 0 ? left : right, secondX = (sprite.Flip & 1) == 0 ? right : left;
            double firstY = (sprite.Flip & 2) == 0 ? top : bottom, secondY = (sprite.Flip & 2) == 0 ? bottom : top;
            var points = new[] { point(firstX, firstY), point(secondX, firstY), point(firstX, secondY) };
            for (int i = 0; i < points.Length; i++) points[i] = new PointF(points[i].X - area.Left, points[i].Y - area.Top);
            using (var image = new Bitmap(area.Width, area.Height, PixelFormat.Format32bppArgb))
            {
                using (var graphics = Graphics.FromImage(image))
                using (var attributes = new ImageAttributes())
                {
                    graphics.InterpolationMode = InterpolationMode.NearestNeighbor; graphics.PixelOffsetMode = PixelOffsetMode.Half;
                    attributes.SetColorMatrix(new ColorMatrix { Matrix00 = Clamp(p.Color.X) / 255f, Matrix11 = Clamp(p.Color.Y) / 255f,
                        Matrix22 = Clamp(p.Color.Z) / 255f, Matrix33 = Clamp(p.Alpha) / 255f * sprite.Opacity / 100f });
                    var source = sprite.Image;
                    graphics.DrawImage(source, points, new RectangleF(0, 0, source.Width, source.Height), GraphicsUnit.Pixel, attributes);
                }
                EffectBlend.Apply(target, image, area, p.Spec.Text("CreamBlendMode", "NORMAL"));
            }
        }
        private static int Clamp(double value) => (int)Math.Max(0, Math.Min(255, value));
        private static bool SupportedBlend(string value) => new[] { "NORMAL", "COPY", "BLEND", "ADD", "MULT", "INVMULT", "INVERSE" }.Contains(value);
    }
}
