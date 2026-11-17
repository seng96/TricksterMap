using TricksterMap.Tests.Cli;

namespace TricksterMap.Tests.Format;

public class CliOptionsPreservationTests
{
    [Fact]
    public void Point_update_preserves_unexposed_options()
    {
        using var fixture = TestMapFixture.Create();
        var service = new MapDocumentService();
        var doc = service.Load(fixture.Md3Path);
        doc.Map.PointObjects[0].Options = unchecked((int)0x87654321);
        service.Save(doc);
        var result = CliRunner.Run("point", "update", "--file", fixture.Md3Path, "--index", "0",
            "--type", "3", "--id", "10", "--map-id", "0", "--x", "120", "--y", "180");
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(unchecked((int)0x87654321), service.Load(fixture.Md3Path).Map.PointObjects[0].Options);
    }

    [Fact]
    public void Range_update_preserves_unexposed_options()
    {
        using var fixture = TestMapFixture.Create();
        var service = new MapDocumentService();
        var doc = service.Load(fixture.Md3Path);
        doc.Map.RangeObjects[0].Options = 87654321;
        service.Save(doc);
        var result = CliRunner.Run("range", "update", "--file", fixture.Md3Path, "--index", "0",
            "--type", "9", "--id", "7", "--destination", "0", "--x1", "10", "--y1", "20", "--x2", "30", "--y2", "40");
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(87654321, service.Load(fixture.Md3Path).Map.RangeObjects[0].Options);
    }
}
