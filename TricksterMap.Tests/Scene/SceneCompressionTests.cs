using System.IO.Compression;
using TricksterMap.Scene;

namespace TricksterMap.Tests.Scene;

public class SceneCompressionTests
{
    [Theory]
    [InlineData(5551, true)] [InlineData(5552, false)] [InlineData(5553, true)]
    [InlineData(65520, false)] [InlineData(65521, true)] [InlineData(65522, false)]
    [InlineData(1048576, true)] [InlineData(1048577, false)]
    public void New_and_edited_compression_pass_independent_platform_zlib_checksum_verification(int length, bool random)
    {
        var bytes = new byte[length];
        if (random) new Random(1412).NextBytes(bytes); else Array.Fill(bytes, (byte)251);
        var envelope = SceneEnvelope.Create(bytes, 0xc0d0, true);
        VerifyPlatformZlib(envelope.Serialize(bytes), bytes);
        bytes[length - 1] ^= 0x97;
        byte[] edited = envelope.Serialize(bytes);
        VerifyPlatformZlib(edited, bytes);
        Assert.Equal(bytes, SceneEnvelope.Unpack(edited, 0xc0d0));
    }

    [Fact]
    public void Optional_real_sq00_tile_edit_compresses_and_reopens_with_valid_checksum()
    {
        string? root = Environment.GetEnvironmentVariable("MEMTS_SCENE_CORPUS");
        if (string.IsNullOrEmpty(root)) return;
        string source = Path.Combine(root, "map_sq00.md3");
        var map = new MapDocumentService().Load(source); var scene = SceneDocument.Load(map);
        var layer = scene.Layers.First(item => item.Kind == 2 && item.Width > 0 && item.Height > 0);
        uint before = layer.GetTile(0, 0);
        layer.SetTile(0, 0, before ^ 0x01000000);
        byte[] edited = scene.SerializeLayers();
        Assert.NotEqual(File.ReadAllBytes(map.LyrPath), edited);
        byte[] decoded = SceneEnvelope.Unpack(edited, 0xc0d0);
        VerifyPlatformZlib(edited, decoded);
        string directory = Path.Combine(Path.GetTempPath(), "TricksterMap.Compression", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "edited.md3");
            File.WriteAllBytes(Path.ChangeExtension(path, ".lyr"), edited);
            File.WriteAllBytes(Path.ChangeExtension(path, ".til"), scene.SerializeTiles());
            var reopened = SceneDocument.Load(new MapDocument(path, map.Map));
            Assert.Equal(before ^ 0x01000000, reopened.Layers[scene.Layers.IndexOf(layer)].GetTile(0, 0));
        }
        finally { Directory.Delete(directory, true); }
    }

    private static void VerifyPlatformZlib(byte[] wrapped, byte[] expected)
    {
        Assert.Equal(wrapped.Length - 12, BitConverter.ToInt32(wrapped, 8));
        using var input = new MemoryStream(wrapped, 12, wrapped.Length - 12, false);
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream(); zlib.CopyTo(output);
        Assert.Equal(expected, output.ToArray());
    }
}
