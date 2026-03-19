using TricksterMap.Data;

namespace TricksterMap
{
    public sealed class MapDocument
    {
        public MapDocument(string md3Path, MapDataInfo map)
        {
            Md3Path = md3Path;
            Map = map;
        }

        public string Md3Path { get; }

        public MapDataInfo Map { get; }

        public string BacPath => System.IO.Path.ChangeExtension(Md3Path, ".bac");

        public string TilPath => System.IO.Path.ChangeExtension(Md3Path, ".til");

        public string LyrPath => System.IO.Path.ChangeExtension(Md3Path, ".lyr");
    }
}
