using System;
using System.IO;

namespace TricksterMap
{
    public sealed class MapDocumentService
    {
        public MapDocument Load(string md3Path)
        {
            if (!File.Exists(md3Path))
            {
                throw new FileNotFoundException("The specified map file does not exist.", md3Path);
            }

            using (var fileStream = File.Open(md3Path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new BinaryReader(fileStream))
            {
                var map = MapDataLoader.Load(reader);
                var document = new MapDocument(md3Path, map);

                if (File.Exists(document.BacPath))
                {
                    map.BacFileSize = (int)new FileInfo(document.BacPath).Length;
                }

                if (File.Exists(document.TilPath))
                {
                    map.TilFileSize = (int)new FileInfo(document.TilPath).Length;
                }

                if (File.Exists(document.LyrPath))
                {
                    map.LyrFileSize = (int)new FileInfo(document.LyrPath).Length;
                }

                return document;
            }
        }

        public void Save(MapDocument document)
        {
            EnsureCompanionFileExists(document.BacPath);
            EnsureCompanionFileExists(document.TilPath);
            EnsureCompanionFileExists(document.LyrPath);

            document.Map.BacFileSize = (int)new FileInfo(document.BacPath).Length;
            document.Map.TilFileSize = (int)new FileInfo(document.TilPath).Length;
            document.Map.LyrFileSize = (int)new FileInfo(document.LyrPath).Length;

            MapSaveHelper.Save(document.Md3Path, document.Map);
        }

        private static void EnsureCompanionFileExists(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    string.Format("Required companion file is missing: {0}", Path.GetFileName(path)),
                    path);
            }
        }
    }
}
