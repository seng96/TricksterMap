using TricksterMap.Data;

namespace TricksterMap.Tests.Editing;

public sealed class MapBundleExportServiceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "TricksterMap.ExportTests", Guid.NewGuid().ToString("N"));
    public MapBundleExportServiceTests() { Directory.CreateDirectory(root); }
    public void Dispose() { Directory.Delete(root, true); }

    private MapDocument Create(bool withTiles = true)
    {
        var path = Path.Combine(root, "source.md3");
        var map = new MapDataInfo { Version = 303, TileBpp = 16, TileSizeX = 32, TileSizeY = 32, TileGroupCount = withTiles ? 1 : 0 };
        map.ConfigLayers.Add(new ConfigLayer { Type = 1, X = 2, Y = 1, BppX = 16, BppY = 16, Data = new byte[] { 0, 2 } });
        foreach (var ext in withTiles ? new[] { ".bac", ".lyr", ".til", ".eff" } : new[] { ".bac", ".lyr", ".eff" })
            File.WriteAllBytes(Path.ChangeExtension(path, ext), new byte[] { 10, 20, 30 });
        MapSaveHelper.Save(path, map);
        return new MapDocumentService().Load(path);
    }

    [Fact]
    public void Export_preserves_source_and_all_companions_and_saves_edits()
    {
        var source = Create(); var original = File.ReadAllBytes(source.Md3Path);
        source.Map.ConfigLayers[0].Data[0] = 1;
        string target = Path.Combine(root, "newmap.md3");
        new MapBundleExportService().Export(source, target);
        Assert.Equal(original, File.ReadAllBytes(source.Md3Path));
        Assert.Equal(1, new MapDocumentService().Load(target).Map.ConfigLayers[0].Data[0]);
        foreach (var ext in new[] { ".bac", ".lyr", ".til", ".eff" })
            Assert.Equal(File.ReadAllBytes(Path.ChangeExtension(source.Md3Path, ext)), File.ReadAllBytes(Path.ChangeExtension(target, ext)));
        Assert.Empty(Directory.GetDirectories(root));
    }

    [Fact]
    public void Conflict_is_rejected_before_any_output_is_created()
    {
        var source = Create(); string target = Path.Combine(root, "newmap.md3");
        File.WriteAllText(Path.ChangeExtension(target, ".lyr"), "existing");
        Assert.Throws<IOException>(() => new MapBundleExportService().Export(source, target));
        Assert.False(File.Exists(target)); Assert.False(File.Exists(Path.ChangeExtension(target, ".bac")));
        Assert.Equal("existing", File.ReadAllText(Path.ChangeExtension(target, ".lyr")));
        Assert.Throws<IOException>(() => new MapBundleExportService().Export(source, source.Md3Path));
    }

    [Fact]
    public void No_tile_map_can_export_without_til_but_missing_bac_is_rejected()
    {
        var source = Create(false); string target = Path.Combine(root, "newmap.md3");
        new MapBundleExportService().Export(source, target);
        Assert.True(File.Exists(target)); Assert.False(File.Exists(Path.ChangeExtension(target, ".til")));
        File.Delete(source.BacPath);
        Assert.Throws<FileNotFoundException>(() => new MapBundleExportService().Export(source, Path.Combine(root, "other.md3")));
        Assert.False(File.Exists(Path.Combine(root, "other.md3")));
    }
}
