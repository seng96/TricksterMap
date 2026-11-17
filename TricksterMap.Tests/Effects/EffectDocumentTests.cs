using System.Text;
using TricksterMap.Effects;

namespace TricksterMap.Tests.Effects;

public class EffectDocumentTests
{
    [Fact]
    public void Legacy_command_uses_registration_name_and_loop_uses_current_tail_time()
    {
        var document = EffectDocument.Parse(Encoding.UTF8.GetBytes("<CAKEWAPPER><CAKE Name='x'><SUBCREAM Name='_LOOP_' Time='1'><SubCreamName/></SUBCREAM><SUBCREAM Name='later' Time='5'><SubCreamName>c</SubCreamName></SUBCREAM></CAKE><CREAM Name='c'/></CAKEWAPPER>"));
        Assert.Empty(document.Warnings); var cake = document.Cakes["x"];
        Assert.Single(cake.Children); Assert.Equal("c", cake.Children[0].Name);
        Assert.Single(cake.Plays); Assert.Equal("PlayLoop", cake.Plays[0].Mode); Assert.Equal(5, cake.Plays[0].Time);
    }
    [Fact]
    public void Cream_reference_is_not_interchangeable_with_registration_command_and_stop_keeps_own_time()
    {
        var document = EffectDocument.Parse(Encoding.UTF8.GetBytes("<CAKEWAPPER><CAKE Name='x'><SUBCREAM Name='ordinary' Time='0'><SubCreamName>_STOP_</SubCreamName></SUBCREAM><SUBCREAM Name='_STOP_' Time='2'><SubCreamName/></SUBCREAM><SUBCREAM Name='later' Time='8'><SubCreamName>_STOP_</SubCreamName></SUBCREAM></CAKE><CREAM Name='_STOP_'/></CAKEWAPPER>"));
        Assert.Empty(document.Warnings); var cake = document.Cakes["x"];
        Assert.Equal(2, cake.Children.Count); Assert.All(cake.Children, item => Assert.Equal("_STOP_", item.Name));
        Assert.Single(cake.Plays); Assert.Equal("PlayStop", cake.Plays[0].Mode); Assert.Equal(2, cake.Plays[0].Time);
    }
    [Fact]
    public void Dtd_and_external_entity_are_rejected()
    {
        var raw = Encoding.UTF8.GetBytes("<!DOCTYPE CAKEWAPPER [<!ENTITY x SYSTEM 'file:///secret'>]><CAKEWAPPER>&x;</CAKEWAPPER>");
        Assert.Throws<System.Xml.XmlException>(() => EffectDocument.Parse(raw));
    }
    [Fact]
    public void Ambiguous_names_are_not_selected_and_unknown_fields_and_order_survive_reference_rewrite()
    {
        var raw = Encoding.UTF8.GetBytes("<CAKEWAPPER CreamCount='2' NoriFileName='old.nri' CakeCount='0'><Unknown token='keep'/><CREAM Name='x'><StartSize>10</StartSize><StartSizeX>0</StartSizeX><EndSizeX>2</EndSizeX><EndSize>20</EndSize></CREAM><CREAM Name='x'/></CAKEWAPPER>");
        var document = EffectDocument.Parse(raw); Assert.Empty(document.Creams); Assert.Single(document.Warnings);
        Assert.Equal(raw, document.WithMaterialReference("old.nri"));
        var rewritten = Encoding.UTF8.GetString(document.WithMaterialReference("data\\map\\new.nri"));
        Assert.True(rewritten.IndexOf("CreamCount", StringComparison.Ordinal) < rewritten.IndexOf("NoriFileName", StringComparison.Ordinal));
        Assert.True(rewritten.IndexOf("NoriFileName", StringComparison.Ordinal) < rewritten.IndexOf("CakeCount", StringComparison.Ordinal));
        Assert.Contains("token=\"keep\"", rewritten); Assert.Equal(raw, document.Original);
    }
    [Fact]
    public void Xml_ordered_scalar_and_axis_zero_assignment_match_native_setters()
    {
        var document = EffectDocument.Parse(Encoding.UTF8.GetBytes("<CAKEWAPPER><CREAM Name='x'><StartSize>10</StartSize><StartSizeX>0</StartSizeX><EndSizeX>2</EndSizeX><EndSize>20</EndSize></CREAM></CAKEWAPPER>"));
        var cream = document.Creams["x"]; Assert.Equal(new[] { 0.0, 10 }, cream.OrderedXY("StartSize", 100)); Assert.Equal(new[] { 20.0, 20 }, cream.OrderedXY("EndSize", 100));
    }
    [Fact]
    public void Placement_keeps_opaque_bytes_and_uses_exact_32_byte_name_field()
    {
        var raw = Enumerable.Repeat((byte)0xcc, 52).ToArray(); BitConverter.GetBytes(13).CopyTo(raw, 4); BitConverter.GetBytes(27).CopyTo(raw, 8);
        Encoding.ASCII.GetBytes("fountain\0").CopyTo(raw, 20); var placement = new MapEffectInstance(raw);
        Assert.Equal("fountain", placement.Name); Assert.Equal(13, placement.X); Assert.Equal(27, placement.Y); Assert.Equal(raw, placement.RawData);
    }
    [Theory]
    [InlineData("../fx.nri")][InlineData("data/../fx.nri")][InlineData("C:/private/fx.nri")][InlineData("data/map/*.nri")]
    public void Unsafe_or_unmappable_material_paths_are_not_followed(string path)
    { Assert.Equal("invalid", EffectResourceResolver.Resolve(path, null).Status); }
    [Fact]
    public void Historical_author_path_resolves_only_declared_data_suffix_and_explicit_root_wins()
    {
        string folder = Path.Combine(Path.GetTempPath(), "Effects-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(folder, "data/map")); string nri = Path.Combine(folder, "data/map/fx.nri"); File.WriteAllBytes(nri, new byte[] { 1 });
            var result = EffectResourceResolver.Resolve("D:/datasafe/Trickster/data/map/fx.nri", Path.Combine(folder, "data/map/test.eff"));
            Assert.True(result.IsResolved); Assert.True(result.Remapped); Assert.Equal(Path.GetFullPath(nri), result.ResolvedPath);
            Directory.CreateDirectory(Path.Combine(folder, "alternate/data/map")); string other = Path.Combine(folder, "alternate/data/map/fx.nri"); File.WriteAllBytes(other, new byte[] { 2 });
            var explicitRoot = EffectResourceResolver.Resolve("data/map/fx.nri", Path.Combine(folder, "data/map/test.eff"), Path.Combine(folder, "alternate"));
            Assert.Equal(Path.GetFullPath(other), explicitRoot.ResolvedPath);
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }
}
