using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using TricksterMap.Effects;
using TricksterMap.Tests.Scene;

namespace TricksterMap.Tests.Effects;

public class MapResourceArchiveTests
{
    [Fact]
    public void Existing_material_without_the_declared_texture_refuses_portable_export()
    {
        using var fixture = new SceneFixture(303, true); File.WriteAllBytes(fixture.Map.BacPath, new byte[] { 1 });
        var nri = new byte[84]; Encoding.ASCII.GetBytes("NORI").CopyTo(nri, 0); Encoding.ASCII.GetBytes("GAWI").CopyTo(nri, 40);
        BitConverter.GetBytes(302).CopyTo(nri, 4); BitConverter.GetBytes(16).CopyTo(nri, 48);
        BitConverter.GetBytes(40).CopyTo(nri, 32); BitConverter.GetBytes(84).CopyTo(nri, 36);
        File.WriteAllBytes(Path.Combine(fixture.Root, "fx.nri"), nri);
        File.WriteAllText(Path.ChangeExtension(fixture.Map.Md3Path, ".eff"),
            "<CAKEWAPPER NoriFileName='data/map/fx.nri'><CREAM Name='spark'><TextureName>missing</TextureName></CREAM></CAKEWAPPER>");
        var report = EffectDependencyReport.Inspect(fixture.Map);
        Assert.True(report.Material.IsResolved); Assert.NotNull(report.MaterialIndex); Assert.True(report.HasBlockingIssues);
        Assert.Contains(report.Warnings, warning => warning.Contains("missing"));
        string output = Path.Combine(fixture.Root, "new.zip");
        Assert.Throws<InvalidDataException>(() => new MapResourceArchiveService().Export(fixture.Map, output)); Assert.False(File.Exists(output));
    }
    [Fact]
    public void Missing_material_refuses_export_without_partial_outputs()
    {
        using var fixture = new SceneFixture(303, true); File.WriteAllBytes(fixture.Map.BacPath, new byte[] { 1 });
        File.WriteAllText(Path.ChangeExtension(fixture.Map.Md3Path, ".eff"), "<CAKEWAPPER NoriFileName='data/map/missing.nri'/>");
        string output = Path.Combine(fixture.Root, "new.zip");
        Assert.Throws<InvalidDataException>(() => new MapResourceArchiveService().Export(fixture.Map, output)); Assert.False(File.Exists(output));
        Assert.Empty(Directory.GetDirectories(fixture.Root, ".TricksterMap-resources-*"));
    }
    [Fact]
    public void Portable_zip_preserves_material_bytes_and_rewrites_only_exported_effect_reference()
    {
        using var fixture = new SceneFixture(303, true); File.WriteAllBytes(fixture.Map.BacPath, new byte[] { 1 });
        var nri = new byte[84]; Encoding.ASCII.GetBytes("NORI").CopyTo(nri, 0); Encoding.ASCII.GetBytes("GAWI").CopyTo(nri, 40);
        BitConverter.GetBytes(302).CopyTo(nri, 4); BitConverter.GetBytes(16).CopyTo(nri, 48);
        BitConverter.GetBytes(40).CopyTo(nri, 32); BitConverter.GetBytes(84).CopyTo(nri, 36);
        File.WriteAllBytes(Path.Combine(fixture.Root, "fx.nri"), nri);
        string effect = Path.ChangeExtension(fixture.Map.Md3Path, ".eff");
        var xml = Encoding.UTF8.GetBytes("<CAKEWAPPER CreamCount='0' NoriFileName='D:/author/data/map/fx.nri' CakeCount='0'><Unknown keep='yes'/></CAKEWAPPER>");
        File.WriteAllBytes(effect, xml); string output = Path.Combine(fixture.Root, "new.zip");
        new MapResourceArchiveService().Export(fixture.Map, output);
        using var archive = ZipFile.OpenRead(output);
        Assert.NotNull(archive.GetEntry("data/map/new.md3")); Assert.NotNull(archive.GetEntry("data/map/new.lyr")); Assert.NotNull(archive.GetEntry("data/map/new.til"));
        using var stream = archive.GetEntry("data/map/fx.nri")!.Open(); using var memory = new MemoryStream(); stream.CopyTo(memory); Assert.Equal(nri, memory.ToArray());
        using var eff = archive.GetEntry("data/map/new.eff")!.Open(); var exported = XDocument.Load(eff);
        Assert.Equal("data\\map\\fx.nri", (string?)exported.Root!.Attribute("NoriFileName")); Assert.Equal("yes", (string?)exported.Root.Element("Unknown")!.Attribute("keep"));
        Assert.Equal(xml, File.ReadAllBytes(effect)); Assert.NotNull(archive.GetEntry("asset-manifest.xml"));
        Assert.Throws<IOException>(() => new MapResourceArchiveService().Export(fixture.Map, output));
    }
}
