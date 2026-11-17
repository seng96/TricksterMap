using System.Security.Cryptography;
using System.Text.Json;
using TricksterMap.Scene;

namespace TricksterMap.Tests.Scene;

public class SceneRoundTripTests
{
    [Theory]
    [InlineData(301, false)] [InlineData(302, false)] [InlineData(303, true)]
    public void No_op_retains_envelope_prefix_unknown_name_bytes_objects_and_padding(int version, bool compressed)
    {
        using var fixture = new SceneFixture(version, compressed);
        var scene = SceneDocument.Load(fixture.Map);
        Assert.False(scene.IsDirty);
        Assert.Equal(File.ReadAllBytes(fixture.Map.LyrPath), scene.SerializeLayers());
        Assert.Equal(File.ReadAllBytes(fixture.Map.TilPath), scene.SerializeTiles());
        Assert.Equal(0xcc, scene.Layers[0].NameBytes[31]);
        Assert.Equal(version == 301 ? 48 : 56, scene.Layers[0].Objects[0].RawData.Length);
    }

    [Theory]
    [InlineData(301, false)] [InlineData(303, true)]
    public void Edited_companions_rebuild_offsets_and_reopen_without_discarding_opaque_data(int version, bool compressed)
    {
        using var fixture = new SceneFixture(version, compressed);
        var scene = SceneDocument.Load(fixture.Map);
        byte[] originalObject = scene.Layers[0].Objects[0].RawData.ToArray();
        scene.Layers[0].SetTile(1, 1, 0x31000000);
        scene.Layers[0].Objects[0].X = 1234;
        scene.TileGroups[0].Pixels[17] = 99;
        File.WriteAllBytes(fixture.Map.LyrPath, scene.SerializeLayers());
        File.WriteAllBytes(fixture.Map.TilPath, scene.SerializeTiles());
        var reopened = SceneDocument.Load(fixture.Map);
        Assert.Equal(0x31000000u, reopened.Layers[0].GetTile(1, 1));
        Assert.Equal(1234, reopened.Layers[0].Objects[0].X);
        Assert.Equal(originalObject[16..], reopened.Layers[0].Objects[0].RawData[16..]);
        Assert.Equal(new byte[] { 76, 77 }, reopened.Layers[0].TrailingData);
        Assert.Equal(new byte[] { 83, 84 }, reopened.TileGroups[0].TrailingData);
        Assert.Equal(99, reopened.TileGroups[0].Pixels[17]);
        Assert.False(reopened.IsDirty);
    }

    [Fact]
    public void Optional_real_scene_corpus_round_trips_all_companions()
    {
        string? root = Environment.GetEnvironmentVariable("MEMTS_SCENE_CORPUS");
        if (string.IsNullOrEmpty(root)) return;
        int count = 0; var rows = new List<object>();
        foreach (string path in Directory.EnumerateFiles(root, "*.md3").OrderBy(x => x))
        {
            using var file = File.OpenRead(path); var signature = new byte[4]; file.ReadExactly(signature);
            if (!signature.SequenceEqual("MDN_"u8.ToArray()) || !File.Exists(Path.ChangeExtension(path, ".lyr"))) continue;
            var map = new MapDocumentService().Load(path);
            SceneDocument scene;
            try { scene = SceneDocument.Load(map); }
            catch (Exception error) { throw new InvalidOperationException(path, error); }
            byte[] lyr = File.ReadAllBytes(map.LyrPath), til = File.Exists(map.TilPath) ? File.ReadAllBytes(map.TilPath) : Array.Empty<byte>();
            Assert.Equal(lyr, scene.SerializeLayers()); Assert.Equal(til, scene.SerializeTiles());
            rows.Add(new { map = Path.GetFileName(path), version = map.Map.Version, layers = scene.Layers.Count,
                groups = scene.TileGroups.Count, lyr_sha256 = Convert.ToHexStringLower(SHA256.HashData(lyr)),
                til_sha256 = Convert.ToHexStringLower(SHA256.HashData(til)), byte_identical = true });
            count++;
        }
        Assert.True(count > 0);
        string? report = Environment.GetEnvironmentVariable("MEMTS_SCENE_CORPUS_REPORT");
        if (!string.IsNullOrEmpty(report)) File.WriteAllText(report, JsonSerializer.Serialize(new { root, checked_maps = count, results = rows }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
