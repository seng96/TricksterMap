using TricksterMap.Data;
using TricksterMap.Scene;

namespace TricksterMap.Tests.Scene;

public class BlankMapTests
{
    [Fact]
    public void Blank_map_clears_placement_and_opaque_body_but_preserves_independent_template_resources()
    {
        using var fixture = new SceneFixture(303, true); File.WriteAllBytes(fixture.Map.BacPath, new byte[] { 11, 22, 33 });
        var template = fixture.Map.Map;
        template.PointObjects.Add(new PointObject { Type = 3, Id = 7, X = 200 });
        template.RangeObjects.Add(new RangeObject { Type = 4, Id = 2 }); template.EffectObjects.Add(new byte[52]);
        template.TrailingData = new byte[] { 11, 12, 13 }; template.Param21 = 77123;
        template.ConfigLayers.Add(new ConfigLayer { Type = 4, X = 1, Y = 1, BppX = 16, BppY = 16, Data = new byte[] { 5 } });
        byte[] sourceMd3 = File.ReadAllBytes(fixture.Map.Md3Path), sourceLyr = File.ReadAllBytes(fixture.Map.LyrPath), sourceTil = File.ReadAllBytes(fixture.Map.TilPath);
        var result = new BlankMapService().Create(fixture.Map, 128, 96);
        var map = result.Document.Map;
        Assert.Same(result.Document, result.Scene.Document); Assert.Equal(fixture.Map.Md3Path, result.Document.Md3Path);
        Assert.Equal(127, map.MapSizeX); Assert.Equal(95, map.MapSizeY); Assert.Equal(4, map.TileCountX); Assert.Equal(3, map.TileCountY);
        Assert.Single(map.ConfigLayers); Assert.Equal(8, map.ConfigLayers[0].X); Assert.Equal(6, map.ConfigLayers[0].Y);
        Assert.All(map.ConfigLayers[0].Data, value => Assert.Equal(1, value));
        Assert.Empty(map.PointObjects); Assert.Empty(map.RangeObjects); Assert.Empty(map.EffectObjects); Assert.Empty(map.TrailingData);
        Assert.Equal(77123, map.Param21); Assert.Equal(0, map.MapSizeSum); Assert.False(result.IncludeEffects);
        Assert.Equal(2, map.LyrDataCount); Assert.Equal(1, map.Param14); Assert.Equal(6, result.Scene.Layers[map.Param14].Order);
        Assert.All(Enumerable.Range(0, 12), index => Assert.Equal(0xffffu, result.Scene.Layers[0].GetTile(index % 4, index / 4)));
        Assert.Equal(sourceTil, result.Scene.SerializeTiles());
        result.Scene.TileGroups[0].Pixels[0] = 99; Assert.Equal(sourceTil, File.ReadAllBytes(fixture.Map.TilPath));
        Assert.Equal(sourceMd3, File.ReadAllBytes(fixture.Map.Md3Path)); Assert.Equal(sourceLyr, File.ReadAllBytes(fixture.Map.LyrPath));
        Assert.Single(template.PointObjects); Assert.Single(template.EffectObjects);
    }

    [Fact]
    public void Blank_scene_can_be_painted_exported_as_new_files_and_reopened()
    {
        using var fixture = new SceneFixture(303, true); File.WriteAllBytes(fixture.Map.BacPath, new byte[] { 1, 2, 3 });
        var blank = new BlankMapService().Create(fixture.Map, 64, 64);
        new SceneTileEditService(blank.Scene, blank.Scene.Layers[0]).SetCell(1, 1, 0x03000000);
        new ConfigLayerEditService(blank.Document.Map.ConfigLayers[0]).SetCell(2, 3, 0);
        string path = Path.Combine(fixture.Root, "new.md3");
        byte[] lyr = blank.Scene.SerializeLayers(), til = blank.Scene.SerializeTiles();
        blank.Document.Map.BacFileSize = 3; blank.Document.Map.LyrFileSize = lyr.Length; blank.Document.Map.TilFileSize = til.Length;
        File.WriteAllBytes(Path.ChangeExtension(path, ".lyr"), lyr); File.WriteAllBytes(Path.ChangeExtension(path, ".til"), til);
        File.Copy(fixture.Map.BacPath, Path.ChangeExtension(path, ".bac")); MapSaveHelper.Save(path, blank.Document.Map);
        var map = new MapDocumentService().Load(path); var reopened = SceneDocument.Load(map);
        Assert.Equal(0x03000000u, reopened.Layers[0].GetTile(1, 1)); Assert.Equal(0, map.Map.ConfigLayers[0].Data[3 * 4 + 2]);
        Assert.Equal(new FileInfo(path).Length + 3 + lyr.Length + til.Length, map.Map.MapSizeSum);
        Assert.Empty(map.Map.PointObjects); Assert.Empty(map.Map.EffectObjects); Assert.Equal(1, map.Map.Param14);
    }

    [Theory]
    [InlineData(0, 64)] [InlineData(33, 64)] [InlineData(64, -1)] [InlineData(16384, 16384)]
    public void Invalid_blank_dimensions_are_rejected_before_touching_source(int width, int height)
    {
        using var fixture = new SceneFixture(); byte[] original = File.ReadAllBytes(fixture.Map.Md3Path);
        Assert.Throws<ArgumentOutOfRangeException>(() => new BlankMapService().Create(fixture.Map, width, height));
        Assert.Equal(original, File.ReadAllBytes(fixture.Map.Md3Path));
    }
}
