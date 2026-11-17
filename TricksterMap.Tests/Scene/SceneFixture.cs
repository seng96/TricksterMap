using System.Text;
using TricksterMap.Data;
using TricksterMap.Scene;

namespace TricksterMap.Tests.Scene;

internal sealed class SceneFixture : IDisposable
{
    internal string Root { get; } = Path.Combine(Path.GetTempPath(), "TricksterMap.SceneTests", Guid.NewGuid().ToString("N"));
    internal MapDocument Map { get; }
    internal SceneFixture(int version = 303, bool compressed = false)
    {
        Directory.CreateDirectory(Root);
        string path = Path.Combine(Root, "map.md3");
        var map = new MapDataInfo { Version = version, TileBpp = 16, TileSizeX = 32, TileSizeY = 32,
            LyrDataCount = 1, TileGroupCount = 1, MapSizeX = 63, MapSizeY = 63 };
        MapSaveHelper.Save(path, map); Map = new MapDocumentService().Load(path);
        byte[] lyr = Layers(version), til = Tiles();
        File.WriteAllBytes(Map.LyrPath, compressed ? Envelope(lyr, 0xc0d0) : lyr);
        File.WriteAllBytes(Map.TilPath, compressed ? Envelope(til, 0xe0f0) : til);
    }
    private static byte[] Layers(int version)
    {
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        writer.Write(3); writer.Write(new byte[] { 91, 92, 93 });
        var name = new byte[32]; Encoding.ASCII.GetBytes("floor").CopyTo(name, 0); name[31] = 0xcc; writer.Write(name);
        foreach (int value in new[] { 2, 7, 100, 100, -12, 30, 16, 2, 2 }) writer.Write(value);
        foreach (uint word in new uint[] { 0x03000000, 0x10000000, 0xffff, 0x20000000 }) writer.Write(word);
        writer.Write(1);
        byte[] item = Enumerable.Repeat((byte)0xcc, version == 301 ? 48 : 56).ToArray();
        Encoding.ASCII.GetBytes("object\0").CopyTo(item, 16); writer.Write(item);
        writer.Write(new byte[] { 76, 77 }); return stream.ToArray();
    }
    private static byte[] Tiles()
    {
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        writer.Write(2); writer.Write(new byte[] { 81, 82 });
        writer.Write(2048); writer.Write(1); writer.Write(1); writer.Write(new byte[2048]);
        writer.Write(new byte[] { 83, 84 }); return stream.ToArray();
    }
    internal static byte[] Envelope(byte[] bytes, uint magic)
    {
        // Compress through the resource writer, not through a test-specific codec.
        byte[] empty = { (byte)magic, (byte)(magic >> 8), 0, 0, 0, 0, 0, 0, 8, 0, 0, 0, 120, 156, 3, 0, 0, 0, 0, 1 };
        return SceneEnvelope.Decode(empty, magic).Serialize(bytes);
    }
    public void Dispose() => Directory.Delete(Root, true);
}
