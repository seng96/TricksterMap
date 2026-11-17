using TricksterMap.Scene;

namespace TricksterMap.Tests.Scene;

public class SceneValidationTests
{
    [Fact]
    public void Growing_layer_and_group_tables_rebuilds_offsets_counts_and_preserves_other_blocks()
    {
        using var fixture = new SceneFixture(303, true); var scene = SceneDocument.Load(fixture.Map);
        byte[] firstPixels = scene.TileGroups[0].Pixels.ToArray(), originalObject = scene.Layers[0].Objects[0].RawData.ToArray();
        scene.Layers.Add(new SceneLayer { Kind = 0, X = 222, Order = 9 });
        scene.TileGroups.Add(new SceneTileGroup { Width = 1, Height = 1, Pixels = Enumerable.Repeat((byte)73, 2048).ToArray() });
        Assert.Throws<InvalidDataException>(() => scene.ValidateHeaderCounts());
        fixture.Map.Map.LyrDataCount = 2; fixture.Map.Map.TileGroupCount = 2; scene.ValidateHeaderCounts();
        File.WriteAllBytes(fixture.Map.LyrPath, scene.SerializeLayers()); File.WriteAllBytes(fixture.Map.TilPath, scene.SerializeTiles());
        var reopened = SceneDocument.Load(fixture.Map);
        Assert.Equal(2, reopened.Layers.Count); Assert.Equal(222, reopened.Layers[1].X);
        Assert.Equal(originalObject, reopened.Layers[0].Objects[0].RawData);
        Assert.Equal(firstPixels, reopened.TileGroups[0].Pixels); Assert.Equal(73, reopened.TileGroups[1].Pixels[0]);
    }

    [Fact]
    public void Truncated_payload_bad_checksum_and_wrong_wrapper_are_rejected()
    {
        byte[] raw = SceneFixture.Envelope(new byte[100], 0xc0d0);
        Assert.Throws<InvalidDataException>(() => SceneEnvelope.Decode(raw, 0xe0f0));
        Assert.Throws<InvalidDataException>(() => SceneEnvelope.Decode(raw[..^1], 0xc0d0));
        raw[^1] ^= 1;
        Assert.Throws<InvalidDataException>(() => SceneEnvelope.Decode(raw, 0xc0d0));
        using var fixture = new SceneFixture(); var layerBytes = File.ReadAllBytes(fixture.Map.LyrPath);
        BitConverter.GetBytes(int.MaxValue).CopyTo(layerBytes, 7 + 56);
        File.WriteAllBytes(fixture.Map.LyrPath, layerBytes);
        Assert.Throws<InvalidDataException>(() => SceneDocument.Load(fixture.Map));
    }

    [Fact]
    public void A_new_scene_can_be_created_serialized_and_reopened_without_copying_an_existing_companion()
    {
        using var fixture = new SceneFixture();
        var layer = new SceneLayer { Kind = 2, Width = 2, Height = 2, Payload = new byte[16] };
        var group = new SceneTileGroup { Width = 1, Height = 1, Pixels = Enumerable.Repeat((byte)0x33, 2048).ToArray() };
        var created = SceneDocument.Create(fixture.Map, new[] { layer }, new[] { group }, true);
        File.WriteAllBytes(fixture.Map.LyrPath, created.SerializeLayers()); File.WriteAllBytes(fixture.Map.TilPath, created.SerializeTiles());
        var reopened = SceneDocument.Load(fixture.Map);
        Assert.Equal(group.Pixels, reopened.TileGroups[0].Pixels); Assert.Equal(layer.Payload, reopened.Layers[0].Payload);
    }

    [Fact]
    public void Envelope_payload_mutation_is_not_mistaken_for_a_no_op()
    {
        var envelope = SceneEnvelope.Create(new byte[] { 1, 2, 3 }, 0xa0b0, true);
        envelope.Payload[1] = 77;
        Assert.Equal(new byte[] { 1, 77, 3 }, SceneEnvelope.Unpack(envelope.Serialize(envelope.Payload), 0xa0b0));
    }

    [Fact]
    public void Unsupported_kind_and_invalid_bitmap_pixels_are_rejected_before_serialization()
    {
        using var fixture = new SceneFixture(); var scene = SceneDocument.Load(fixture.Map); var layer = scene.Layers[0];
        byte[] source = File.ReadAllBytes(fixture.Map.LyrPath);
        layer.Kind = 3; Assert.Throws<InvalidDataException>(() => scene.SerializeLayers());
        layer.Kind = 1; layer.Width = 3; layer.Height = 3;
        Assert.Throws<InvalidDataException>(() => scene.SerializeLayers());
        fixture.Map.Map.Param3 = 1; layer.Payload = new byte[] { 8, 0, 0, 0, 4, 0, 0, 0 };
        Assert.Throws<InvalidDataException>(() => scene.SerializeLayers());
        Assert.Equal(source, File.ReadAllBytes(fixture.Map.LyrPath));
    }
}
