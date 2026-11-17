using System;
using System.IO;
using System.Linq;
using System.Text;
using TricksterMap.Data;

namespace TricksterMap.Scene
{
    public sealed class BlankSceneResult
    {
        internal BlankSceneResult(MapDocument document, SceneDocument scene, string templatePath)
        { Document = document; Scene = scene; TemplatePath = templatePath; }
        public MapDocument Document { get; }
        public SceneDocument Scene { get; }
        public string TemplatePath { get; }
        public bool IncludeEffects => false;
    }

    /// <summary>Builds an unregistered blank map using a chosen map's BAC/TIL resources.</summary>
    public sealed class BlankMapService
    {
        public const int MaximumDimension = 16384;
        public BlankSceneResult Create(MapDocument template, int width, int height)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));
            if (width < 32 || height < 32 || width > MaximumDimension || height > MaximumDimension ||
                width % 32 != 0 || height % 32 != 0 || (long)width * height > 67108864)
                throw new ArgumentOutOfRangeException("Map dimensions must be multiples of 32, at most 16384 per axis and 64M pixels total.");
            if (template.Map.TileSizeX != 32 || template.Map.TileSizeY != 32 || template.Map.TileBpp != 16)
                throw new InvalidDataException("Blank authoring currently requires 32x32 RGB555 tile resources.");
            if (!File.Exists(template.BacPath)) throw new FileNotFoundException("Template BAC is missing.", template.BacPath);
            var templateScene = SceneDocument.Load(template);
            MapDataInfo map;
            using (var reader = new BinaryReader(new MemoryStream(MapSaveHelper.Serialize(template.Map)))) map = MapDataLoader.Load(reader);
            map.MapSizeX = width - 1; map.MapSizeY = height - 1;
            map.TileCountX = width / 32; map.TileCountY = height / 32;
            map.LyrDataCount = 2; map.TileGroupCount = templateScene.TileGroups.Count;
            // 694/699 supplied maps associate Param14 with Main/Order6; five
            // exceptions exist and the native consumer is still unconfirmed.
            // Use an explicit Main layer for this authoring convention rather
            // than copying an index into layers the blank scene removed.
            map.Param14 = 1;
            map.ConfigLayers.Clear();
            map.ConfigLayers.Add(new ConfigLayer { Type = 1, X = width / 16, Y = height / 16,
                BppX = 16, BppY = 16, Data = Enumerable.Repeat((byte)1, checked(width / 16 * (height / 16))).ToArray() });
            map.PointObjects.Clear(); map.RangeObjects.Clear(); map.EffectObjects.Clear(); map.TrailingData = new byte[0];
            map.MapSizeSum = 0; map.OriginalSerializedLength = null;
            map.BacFileSize = checked((int)new FileInfo(template.BacPath).Length);
            map.TilFileSize = File.Exists(template.TilPath) ? checked((int)new FileInfo(template.TilPath).Length) : 0;
            map.LyrFileSize = File.Exists(template.LyrPath) ? checked((int)new FileInfo(template.LyrPath).Length) : 0;
            var floor = new SceneLayer { Kind = 2, Order = 2, NameBytes = Name("Floor"), Width = width / 32, Height = height / 32,
                Payload = new byte[checked(width / 32 * (height / 32) * 4)] };
            for (int offset = 0; offset < floor.Payload.Length; offset += 4) { floor.Payload[offset] = 255; floor.Payload[offset + 1] = 255; }
            var main = new SceneLayer { Kind = 0, Order = 6, NameBytes = Name("Main") };
            var document = new MapDocument(template.Md3Path, map);
            var scene = SceneDocument.CreateWithTemplateTiles(document, new[] { floor, main }, templateScene);
            return new BlankSceneResult(document, scene, template.Md3Path);
        }
        private static byte[] Name(string value)
        {
            var bytes = new byte[32]; Encoding.ASCII.GetBytes(value).CopyTo(bytes, 0); return bytes;
        }
    }
}
