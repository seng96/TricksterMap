using TricksterMap.Data;

namespace TricksterMap.Tests.Format;

public class MdnSafetyTests
{
    [Theory]
    [InlineData(0)] [InlineData(4)] [InlineData(95)]
    [InlineData(118)] [InlineData(153)] [InlineData(185)]
    public void Truncated_required_sections_are_rejected(int length)
    {
        byte[] source = MapSaveHelper.Serialize(MdnRoundTripTests.Sample());
        Assert.Throws<InvalidDataException>(() => MdnRoundTripTests.Read(source.Take(length).ToArray()));
    }

    [Theory]
    [InlineData(68, -1)] [InlineData(72, int.MaxValue)]
    [InlineData(76, -1)] [InlineData(80, int.MaxValue)]
    [InlineData(100, int.MaxValue)] [InlineData(104, int.MaxValue)]
    public void Invalid_counts_and_layer_products_fail_before_allocation(int offset, int value)
    {
        byte[] source = MapSaveHelper.Serialize(MdnRoundTripTests.Sample());
        BitConverter.GetBytes(value).CopyTo(source, offset);
        Assert.Throws<InvalidDataException>(() => MdnRoundTripTests.Read(source));
    }

    [Fact]
    public void Embedded_resource_layout_is_rejected_before_section_parsing()
    {
        byte[] source = MapSaveHelper.Serialize(MdnRoundTripTests.Sample());
        source[16] = 1;
        Assert.Throws<InvalidDataException>(() => MdnRoundTripTests.Read(source));
        var map = MdnRoundTripTests.Sample();
        map.Param4 = 1;
        Assert.Throws<InvalidDataException>(() => MapSaveHelper.Serialize(map));
    }

    [Fact]
    public void Invalid_document_does_not_truncate_existing_file()
    {
        string directory = Path.Combine(Path.GetTempPath(), "TricksterMap.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "original.md3");
            byte[] original = MapSaveHelper.Serialize(MdnRoundTripTests.Sample());
            File.WriteAllBytes(path, original);
            var map = MdnRoundTripTests.Read(original);
            map.ConfigLayers[0].Data = new byte[1];
            Assert.Throws<InvalidDataException>(() => MapSaveHelper.Save(path, map));
            Assert.Equal(original, File.ReadAllBytes(path));
            Assert.Single(Directory.GetFiles(directory));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void Repeated_atomic_saves_do_not_accumulate_size_delta()
    {
        string directory = Path.Combine(Path.GetTempPath(), "TricksterMap.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "map.md3");
            var map = MdnRoundTripTests.Read(MapSaveHelper.Serialize(MdnRoundTripTests.Sample()));
            map.PointObjects.Add(new PointObject { Type = 3, Id = 4 });
            MapSaveHelper.Save(path, map);
            byte[] first = File.ReadAllBytes(path);
            MapSaveHelper.Save(path, map);
            Assert.Equal(first, File.ReadAllBytes(path));
            Assert.Equal(456813, map.MapSizeSum);
            Assert.Single(Directory.GetFiles(directory));
        }
        finally { Directory.Delete(directory, true); }
    }
}
