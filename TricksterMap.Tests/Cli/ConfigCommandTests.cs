using System.Text;
using TricksterMap.Cli;
using TricksterMap.Data;

namespace TricksterMap.Tests.Cli;

public sealed class ConfigCommandTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "TricksterMap.Config", Guid.NewGuid().ToString("N"));
    private readonly string file;

    public ConfigCommandTests()
    {
        Directory.CreateDirectory(directory);
        file = Path.Combine(directory, "map.md3");
        foreach (string extension in new[] { ".bac", ".til", ".lyr" }) File.WriteAllBytes(Path.ChangeExtension(file, extension), Array.Empty<byte>());
        var map = new MapDataInfo { Version = 303, MapSizeX = 63, MapSizeY = 47 };
        map.ConfigLayers.Add(new ConfigLayer { Type = 1, X = 4, Y = 3, BppX = 16, BppY = 16,
            Data = new byte[] { 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 3 } });
        MapSaveHelper.Save(file, map);
    }

    private (int Code, string Output) Run(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        int result = new CliApplication().Run(args.Concat(new[] { "--file", file }).ToArray(), output, error);
        return (result, output.ToString() + error.ToString());
    }

    [Fact]
    public void Rectangle_changes_only_inclusive_selected_cells_and_preserves_other_values()
    {
        Assert.Equal(0, Run("config", "rect", "--layer", "0", "--x1", "0", "--y1", "1", "--x2", "1", "--y2", "2", "--value", "1").Code);
        var map = new MapDocumentService().Load(file).Map;
        Assert.Equal(new byte[] { 0, 0, 2, 0, 1, 1, 0, 0, 1, 1, 0, 3 }, map.ConfigLayers[0].Data);
    }

    [Theory]
    [InlineData("--x", "4", "--y", "0", "--value", "1")]
    [InlineData("--x", "0", "--y", "3", "--value", "1")]
    [InlineData("--x", "0", "--y", "0", "--value", "256")]
    [InlineData("--x", "0", "--y", "0", "--value", "-1")]
    [InlineData("--x", "0", "--y", "0", "--unknown", "1")]
    public void Invalid_edit_leaves_source_identical(params string[] flags)
    {
        byte[] original = File.ReadAllBytes(file);
        Assert.NotEqual(0, Run(new[] { "config", "set", "--layer", "0" }.Concat(flags).ToArray()).Code);
        Assert.Equal(original, File.ReadAllBytes(file));
    }

    [Fact]
    public void List_reports_stored_grid_and_step_without_changing_file()
    {
        byte[] original = File.ReadAllBytes(file);
        var result = Run("config", "list");
        Assert.Equal(0, result.Code);
        Assert.Contains("Layer 0: Type=1 Grid=4x3 Step=16x16", result.Output);
        Assert.Equal(original, File.ReadAllBytes(file));
    }

    public void Dispose() => Directory.Delete(directory, true);
}
