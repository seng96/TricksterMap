using TricksterMap.Scene;

namespace TricksterMap.Tests.Scene;

public sealed class SceneBundleExportTests
{
    [Theory]
    [InlineData(301, false)] [InlineData(303, true)]
    public void Scene_edits_export_with_current_MDN_and_preserve_source_and_opaque_records(int version, bool compressed)
    {
        using var fixture = new SceneFixture(version, compressed);
        if (!File.Exists(fixture.Map.BacPath)) File.WriteAllBytes(fixture.Map.BacPath, new byte[] { 1, 2 });
        File.WriteAllBytes(Path.ChangeExtension(fixture.Map.Md3Path, ".eff"), new byte[] { 7, 8 });
        var sourceMap = File.ReadAllBytes(fixture.Map.Md3Path);
        var sourceLayers = File.ReadAllBytes(fixture.Map.LyrPath);
        var sourceTiles = File.ReadAllBytes(fixture.Map.TilPath);
        var scene = SceneDocument.Load(fixture.Map);
        byte[] opaque = scene.Layers[0].Objects[0].RawData.ToArray();
        scene.Layers[0].SetTile(1, 1, 0x23000000);
        scene.Layers[0].Objects[0].X = 789;
        string target = Path.Combine(Path.GetDirectoryName(fixture.Map.Md3Path)!, "scene-edited.md3");
        new MapBundleExportService().Export(fixture.Map, target, scene);
        var reopenedMap = new MapDocumentService().Load(target);
        var reopened = SceneDocument.Load(reopenedMap);
        Assert.Equal(0x23000000u, reopened.Layers[0].GetTile(1, 1));
        Assert.Equal(789, reopened.Layers[0].Objects[0].X);
        Assert.Equal(opaque[16..], reopened.Layers[0].Objects[0].RawData[16..]);
        Assert.Equal(sourceMap, File.ReadAllBytes(fixture.Map.Md3Path));
        Assert.Equal(sourceLayers, File.ReadAllBytes(fixture.Map.LyrPath));
        Assert.Equal(sourceTiles, File.ReadAllBytes(fixture.Map.TilPath));
        Assert.Equal(new byte[] { 7, 8 }, File.ReadAllBytes(Path.ChangeExtension(target, ".eff")));
    }

    [Fact]
    public void Newly_imported_tiles_can_create_a_previously_absent_TIL()
    {
        using var fixture = new SceneFixture(303, true);
        File.WriteAllBytes(fixture.Map.BacPath, new byte[] { 1, 2 });
        var scene = SceneDocument.Load(fixture.Map);
        scene.TileGroups.Clear(); fixture.Map.Map.TileGroupCount = 0;
        File.Delete(fixture.Map.TilPath);
        new SceneTileImportService().Append(scene, 1, 1, Enumerable.Repeat((byte)31, 2048).ToArray());
        string target = Path.Combine(fixture.Root, "created-tile-resource.md3");
        new MapBundleExportService().Export(fixture.Map, target, scene);
        var reopened = SceneDocument.Load(new MapDocumentService().Load(target));
        Assert.Equal(scene.TileGroups[0].Pixels, reopened.TileGroups[0].Pixels);
        Assert.False(File.Exists(fixture.Map.TilPath));
    }

    [Fact]
    public void Scene_from_another_document_is_rejected_without_output()
    {
        using var source = new SceneFixture(303, true);
        using var other = new SceneFixture(303, true);
        string target = Path.Combine(Path.GetDirectoryName(source.Map.Md3Path)!, "wrong.md3");
        Assert.Throws<ArgumentException>(() => new MapBundleExportService().Export(source.Map, target, SceneDocument.Load(other.Map)));
        Assert.False(File.Exists(target));
    }
}
