using TricksterMap.Scene;

namespace TricksterMap.Tests.Scene;

public class SceneTileImportTests
{
    [Fact]
    public void Append_copies_pixels_updates_header_and_can_paint_export_and_reopen()
    {
        using var fixture = new SceneFixture(303, true); var scene = SceneDocument.Load(fixture.Map);
        byte[] original = scene.TileGroups[0].Pixels.ToArray(); byte[] pixels = Enumerable.Repeat((byte)137, 4096).ToArray();
        int index = new SceneTileImportService().Append(scene, 2, 1, pixels);
        Assert.Equal(1, index); Assert.Equal(2, fixture.Map.Map.TileGroupCount); Assert.Equal(2, scene.TileGroups.Count);
        pixels[0] = 12; Assert.Equal(137, scene.TileGroups[1].Pixels[0]);
        new SceneTileEditService(scene, scene.Layers[0]).SetCell(0, 1, 0x03010001);
        File.WriteAllBytes(fixture.Map.LyrPath, scene.SerializeLayers()); File.WriteAllBytes(fixture.Map.TilPath, scene.SerializeTiles());
        var reopened = SceneDocument.Load(fixture.Map);
        Assert.Equal(0x03010001u, reopened.Layers[0].GetTile(0, 1)); Assert.Equal(137, reopened.TileGroups[1].Pixels[0]);
        Assert.Equal(original, reopened.TileGroups[0].Pixels);
    }

    [Theory]
    [InlineData(0, 1, 0)] [InlineData(-1, 1, 2048)] [InlineData(65536, 1, 2048)]
    [InlineData(1, 65536, 2048)] [InlineData(1, 1, 2047)]
    public void Invalid_input_is_rejected_before_group_or_header_changes(int width, int height, int bytes)
    {
        using var fixture = new SceneFixture(); var scene = SceneDocument.Load(fixture.Map);
        byte[] before = scene.SerializeTiles(); int groups = fixture.Map.Map.TileGroupCount;
        Assert.ThrowsAny<ArgumentException>(() => new SceneTileImportService().Append(scene, width, height, new byte[bytes]));
        Assert.Equal(groups, scene.TileGroups.Count); Assert.Equal(groups, fixture.Map.Map.TileGroupCount);
        Assert.Equal(before, scene.SerializeTiles());
    }

    [Fact]
    public void Group_256_and_header_mismatch_are_rejected_without_partial_mutation()
    {
        using var fixture = new SceneFixture(); var scene = SceneDocument.Load(fixture.Map); var service = new SceneTileImportService();
        fixture.Map.Map.TileGroupCount = 4;
        Assert.Throws<InvalidDataException>(() => service.Append(scene, 1, 1, new byte[2048])); Assert.Single(scene.TileGroups);
        fixture.Map.Map.TileGroupCount = 1;
        while (scene.TileGroups.Count < 256) service.Append(scene, 1, 1, new byte[2048]);
        Assert.Throws<InvalidOperationException>(() => service.Append(scene, 1, 1, new byte[2048]));
        Assert.Equal(256, scene.TileGroups.Count); Assert.Equal(256, fixture.Map.Map.TileGroupCount);
    }
}
