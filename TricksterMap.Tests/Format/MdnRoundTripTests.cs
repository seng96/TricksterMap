using System.Text.Json;
using System.Security.Cryptography;
using TricksterMap.Data;

namespace TricksterMap.Tests.Format;

public class MdnRoundTripTests
{
    internal static MapDataInfo Sample(int version = 303) => new MapDataInfo {
        Version = version, MapSizeSum = 456789, TileBpp = 16,
        MapSizeX = 31, MapSizeY = 31, TileSizeX = 32, TileSizeY = 32,
        Param21 = unchecked((int)0xcccccccc),
        ConfigLayers = new() { new ConfigLayer { Type = 1, X = 2, Y = 2,
            BppX = 16, BppY = 16, Data = new byte[] { 0, 1, 2, 255 } } },
        RangeObjects = new() { new RangeObject { Type = 9, Id = 8, Options = 99 } },
        PointObjects = new() { new PointObject { Type = 3, Id = 9, Options = 123 } },
        EffectObjects = new() { Enumerable.Range(0, 52).Select(x => (byte)x).ToArray() },
        TrailingData = new byte[] { 0, 255, 17, 33, 64, 0, 99, 1 }
    };

    internal static MapDataInfo Read(byte[] bytes)
    {
        using var reader = new BinaryReader(new MemoryStream(bytes));
        return MapDataLoader.Load(reader);
    }

    [Theory]
    [InlineData(301)] [InlineData(302)] [InlineData(303)]
    public void No_op_preserves_entire_document_and_legacy_aggregate_size(int version)
    {
        byte[] source = MapSaveHelper.Serialize(Sample(version));
        var loaded = Read(source);
        Assert.Equal(456789, loaded.MapSizeSum);
        Assert.Equal(Sample().TrailingData, loaded.TrailingData);
        Assert.Equal(source, MapSaveHelper.Serialize(loaded));
    }

    [Fact]
    public void Added_record_adjusts_original_sum_only_by_size_delta()
    {
        var map = Read(MapSaveHelper.Serialize(Sample()));
        map.PointObjects.Add(new PointObject { Type = 3, Id = 10 });
        byte[] edited = MapSaveHelper.Serialize(map);
        Assert.Equal(456813, BitConverter.ToInt32(edited, 20));
        Assert.Equal(edited, MapSaveHelper.Serialize(Read(edited)));
        Assert.Equal(Sample().TrailingData, Read(edited).TrailingData);
    }

    [Fact]
    public void Optional_real_corpus_round_trips_each_MDN_byte_for_byte()
    {
        var root = Environment.GetEnvironmentVariable("MEMTS_MAP_CORPUS");
        if (string.IsNullOrEmpty(root)) return;
        Assert.True(Directory.Exists(root), "MEMTS_MAP_CORPUS must exist.");
        var rows = new List<object>();
        int checkedCount = 0, skipped = 0, tails = 0;
        string outputDirectory = Path.Combine(Path.GetTempPath(), "TricksterMap.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);
        try
        {
        foreach (string path in Directory.EnumerateFiles(root, "*.md3").OrderBy(x => x))
        {
            byte[] source = File.ReadAllBytes(path);
            if (source.Length < 4 || !source.AsSpan(0, 4).SequenceEqual("MDN_"u8)) { skipped++; continue; }
            var map = Read(source);
            string outputPath = Path.Combine(outputDirectory, "roundtrip.md3");
            MapSaveHelper.Save(outputPath, map);
            byte[] result = File.ReadAllBytes(outputPath);
            bool same = source.SequenceEqual(result);
            rows.Add(new { file = Path.GetFileName(path), version = map.Version, bytes = source.Length,
                trailing_bytes = map.TrailingData.Length, byte_identical = same,
                sha256 = Convert.ToHexStringLower(SHA256.HashData(source)) });
            if (map.TrailingData.Length != 0) tails++;
            Assert.True(same, path + " changed during no-op serialization");
            checkedCount++;
        }
        }
        finally { Directory.Delete(outputDirectory, true); }
        string? report = Environment.GetEnvironmentVariable("MEMTS_MAP_CORPUS_REPORT");
        if (!string.IsNullOrEmpty(report))
            File.WriteAllText(report, JsonSerializer.Serialize(new { root, checkedCount, skipped,
                files_with_trailing_data = tails, results = rows }, new JsonSerializerOptions { WriteIndented = true }));
        Assert.True(checkedCount > 0, "No MDN documents were found.");
    }
}
