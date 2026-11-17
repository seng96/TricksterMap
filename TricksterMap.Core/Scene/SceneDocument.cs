using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace TricksterMap.Scene
{
    public sealed class SceneDocument
    {
        private SceneEnvelope layerEnvelope, tileEnvelope;
        private byte[] layerPrefix, tilePrefix, initialLayers, initialTiles;
        private SceneDocument() { }
        public MapDocument Document { get; private set; }
        public List<SceneLayer> Layers { get; private set; }
        public List<SceneTileGroup> TileGroups { get; private set; }
        public int Version => Document.Map.Version;
        public int TileBpp => Document.Map.TileBpp;
        public int TileWidth => Document.Map.TileSizeX;
        public int TileHeight => Document.Map.TileSizeY;
        public int Width => checked(Document.Map.MapSizeX + 1);
        public int Height => checked(Document.Map.MapSizeY + 1);
        public bool HasTileFile { get; private set; }
        internal int TilePrefixLength => tilePrefix.Length;
        public bool IsDirty => !Same(initialLayers, Hash(EncodeLayers())) || !Same(initialTiles, Hash(EncodeTiles()));

        public static SceneDocument Load(MapDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (document.Map.Version < 301 || document.Map.Version > 303 || document.Map.TileBpp != 16 ||
                document.Map.TileSizeX <= 0 || document.Map.TileSizeY <= 0 ||
                (long)document.Map.TileSizeX * document.Map.TileSizeY * 2 > 1024 * 1024)
                throw new InvalidDataException("Unsupported scene version or tile pixel metadata.");
            var result = new SceneDocument { Document = document, HasTileFile = File.Exists(document.TilPath) };
            byte[] layers = File.Exists(document.LyrPath) ? File.ReadAllBytes(document.LyrPath) :
                (document.Map.LyrDataCount == 0 ? new byte[0] : throw new FileNotFoundException("Scene LYR is missing.", document.LyrPath));
            byte[] tiles = result.HasTileFile ? File.ReadAllBytes(document.TilPath) :
                (document.Map.TileGroupCount == 0 ? new byte[0] : throw new FileNotFoundException("Scene TIL is missing.", document.TilPath));
            result.layerEnvelope = SceneEnvelope.Decode(layers, 0xc0d0);
            result.tileEnvelope = SceneEnvelope.Decode(tiles, 0xe0f0);
            result.Layers = SceneLayerCodec.Read(result.layerEnvelope.Payload, document.Map.LyrDataCount, document.Map.Version, out result.layerPrefix);
            result.TileGroups = SceneTileCodec.Read(result.tileEnvelope.Payload, document.Map.TileGroupCount,
                checked(document.Map.TileSizeX * document.Map.TileSizeY * 2), out result.tilePrefix);
            result.initialLayers = Hash(result.EncodeLayers()); result.initialTiles = Hash(result.EncodeTiles());
            return result;
        }

        /// <summary>Creates companion models for a new scene; callers must also create MDN and BAC resources.</summary>
        public static SceneDocument Create(MapDocument document, IEnumerable<SceneLayer> layers,
            IEnumerable<SceneTileGroup> groups, bool compressed)
        {
            if (document == null || layers == null || groups == null) throw new ArgumentNullException("Scene inputs cannot be null.");
            if (document.Map.Version < 301 || document.Map.Version > 303 || document.Map.TileBpp != 16 ||
                document.Map.TileSizeX <= 0 || document.Map.TileSizeY <= 0)
                throw new InvalidDataException("Unsupported scene version or tile pixel metadata.");
            if (compressed != (document.Map.Version >= 303))
                throw new InvalidDataException("New scene compression must follow the native version branch: raw for 301/302, wrapped for 303.");
            var result = new SceneDocument { Document = document, Layers = layers.ToList(), TileGroups = groups.ToList(),
                layerPrefix = new byte[0], tilePrefix = new byte[0], HasTileFile = true };
            result.ValidateHeaderCounts();
            byte[] lyr = result.EncodeLayers(), til = result.EncodeTiles();
            result.layerEnvelope = SceneEnvelope.Create(lyr, 0xc0d0, compressed);
            result.tileEnvelope = SceneEnvelope.Create(til, 0xe0f0, compressed);
            result.initialLayers = Hash(lyr); result.initialTiles = Hash(til);
            return result;
        }

        /// <summary>New layers with independent tile data, retaining the template TIL envelope byte-for-byte.</summary>
        public static SceneDocument CreateWithTemplateTiles(MapDocument document, IEnumerable<SceneLayer> layers, SceneDocument template)
        {
            if (template == null || document == null || layers == null) throw new ArgumentNullException("Scene inputs cannot be null.");
            if (document.Map.Version != template.Version || document.Map.TileBpp != template.TileBpp ||
                document.Map.TileSizeX != template.TileWidth || document.Map.TileSizeY != template.TileHeight)
                throw new InvalidDataException("Template tile pixel format differs from the new scene.");
            var result = new SceneDocument { Document = document, Layers = layers.ToList(),
                TileGroups = template.TileGroups.Select(group => new SceneTileGroup {
                    Width = group.Width, Height = group.Height, Pixels = (byte[])group.Pixels.Clone(),
                    TrailingData = (byte[])group.TrailingData.Clone() }).ToList(),
                layerPrefix = new byte[0], tilePrefix = (byte[])template.tilePrefix.Clone(), HasTileFile = template.HasTileFile };
            result.ValidateHeaderCounts();
            byte[] lyr = result.EncodeLayers();
            result.layerEnvelope = SceneEnvelope.Create(lyr, 0xc0d0, document.Map.Version >= 303);
            result.tileEnvelope = SceneEnvelope.Decode(template.SerializeTiles(), 0xe0f0);
            result.initialLayers = Hash(lyr); result.initialTiles = Hash(result.EncodeTiles());
            return result;
        }

        public byte[] SerializeLayers()
        {
            byte[] payload = EncodeLayers();
            return layerEnvelope.Serialize(Same(initialLayers, Hash(payload)) ? layerEnvelope.Payload : payload);
        }
        public byte[] SerializeTiles()
        {
            byte[] payload = EncodeTiles();
            return tileEnvelope.Serialize(Same(initialTiles, Hash(payload)) ? tileEnvelope.Payload : payload);
        }
        public void ValidateHeaderCounts()
        {
            if (Layers.Count != Document.Map.LyrDataCount || TileGroups.Count != Document.Map.TileGroupCount)
                throw new InvalidDataException("Scene layer/group counts differ from MDN header. Update them together before export.");
        }
        private byte[] EncodeLayers()
        {
            foreach (var layer in Layers)
                if (layer != null && layer.Kind == 1 && layer.Payload != null)
                {
                    if (Document.Map.Param3 != 0 && Document.Map.Param3 != 1)
                        throw new InvalidDataException("Unsupported scene bitmap compression mode.");
                    SceneBitmapValidator.Validate(layer.Payload, layer.Width, layer.Height, Document.Map.Param3 == 1);
                }
            return SceneLayerCodec.Write(Layers, Version, layerPrefix);
        }
        private byte[] EncodeTiles() => SceneTileCodec.Write(TileGroups, checked(TileWidth * TileHeight * 2), tilePrefix);
        private static byte[] Hash(byte[] bytes) { using (var sha = SHA256.Create()) return sha.ComputeHash(bytes); }
        private static bool Same(byte[] left, byte[] right) => left.SequenceEqual(right);
    }
}
