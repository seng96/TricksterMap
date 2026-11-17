using System;
using System.Collections.Generic;
using System.IO;
using TricksterMap.Scene;

namespace TricksterMap
{
    /// <summary>Exports a fresh map bundle, preserving companions byte-for-byte.</summary>
    public sealed class MapBundleExportService
    {
        public void Export(MapDocument source, string destination)
        {
            Export(source, destination, null);
        }

        public void Export(MapDocument source, string destination, SceneDocument scene, bool includeEffects = true)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            byte[] editedLayers = null, editedTiles = null;
            if (scene != null)
            {
                if (!ReferenceEquals(scene.Document.Map, source.Map))
                    throw new ArgumentException("Scene belongs to another map.");
                scene.ValidateHeaderCounts();
                editedLayers = scene.SerializeLayers();
                editedTiles = scene.SerializeTiles();
            }
            string target = Path.GetFullPath(destination);
            if (!string.Equals(Path.GetExtension(target), ".md3", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Destination must end in .md3.");
            string parent = Path.GetDirectoryName(target);
            if (!Directory.Exists(parent)) throw new DirectoryNotFoundException(parent);
            var extensions = new List<string> { ".md3", ".bac", ".lyr" };
            if (source.Map.TileGroupCount > 0 || File.Exists(source.TilPath)) extensions.Add(".til");
            if (includeEffects && File.Exists(Path.ChangeExtension(source.Md3Path, ".eff"))) extensions.Add(".eff");
            foreach (string extension in extensions)
            {
                string input = Path.ChangeExtension(source.Md3Path, extension);
                string output = Path.ChangeExtension(target, extension);
                bool generated = extension == ".lyr" && editedLayers != null || extension == ".til" && editedTiles != null;
                if (!File.Exists(input) && !generated) throw new FileNotFoundException("Required companion file is missing.", input);
                if (string.Equals(Path.GetFullPath(input), output, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Export must use a new map name or directory.");
                if (File.Exists(output) || Directory.Exists(output)) throw new IOException("Export will not overwrite: " + output);
            }

            string staging = Path.Combine(parent, ".TricksterMap-export-" + Guid.NewGuid().ToString("N"));
            var ownedOutputs = new List<string>();
            Directory.CreateDirectory(staging);
            try
            {
                string stagedMap = Path.Combine(staging, Path.GetFileName(target));
                foreach (string extension in extensions)
                    if (extension != ".md3")
                    {
                        byte[] replacement = extension == ".lyr" ? editedLayers : extension == ".til" ? editedTiles : null;
                        string output = Path.ChangeExtension(stagedMap, extension);
                        if (replacement == null) File.Copy(Path.ChangeExtension(source.Md3Path, extension), output);
                        else File.WriteAllBytes(output, replacement);
                    }
                // Serialize a snapshot so exporting (or a later failed move) never
                // changes the source model's header/baseline bookkeeping.
                Data.MapDataInfo stagedModel;
                using (var reader = new BinaryReader(new MemoryStream(MapSaveHelper.Serialize(source.Map))))
                    stagedModel = MapDataLoader.Load(reader);
                long companionDelta = editedLayers == null ? 0 :
                    (long)editedLayers.Length - ExistingLength(source.LyrPath);
                if (editedTiles != null && extensions.Contains(".til"))
                    companionDelta += (long)editedTiles.Length - ExistingLength(source.TilPath);
                stagedModel.MapSizeSum = checked((int)(stagedModel.MapSizeSum + companionDelta));
                MapSaveHelper.Save(stagedMap, stagedModel);
                foreach (string extension in extensions)
                {
                    string output = Path.ChangeExtension(target, extension);
                    File.Move(Path.ChangeExtension(stagedMap, extension), output);
                    ownedOutputs.Add(output);
                }
            }
            catch
            {
                foreach (string output in ownedOutputs) File.Delete(output);
                throw;
            }
            finally
            {
                // Only the exact unique directory created above belongs to this operation.
                Directory.Delete(staging, true);
            }
        }

        private static long ExistingLength(string path) => File.Exists(path) ? new FileInfo(path).Length : 0;
    }
}
