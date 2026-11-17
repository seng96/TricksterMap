using TricksterMap.Scene;

namespace TricksterMap.Tests.Scene;

public class ObjectAddTests
{
    [Theory]
    [InlineData(301)]
    [InlineData(302)]
    [InlineData(303)]
    public void Add_to_empty_layer_copies_asset_name_and_round_trips_with_undo(int version)
    {
        using var fixture = new SceneFixture(version, version == 303);
        var scene = SceneDocument.Load(fixture.Map); var layer = scene.Layers[0]; layer.Objects.Clear();
        var edit = new SceneObjectEditService(layer);
        var item = new SceneObject(new byte[version == 301 ? 48 : 56]);
        byte[] name = Enumerable.Range(0, 32).Select(i => (byte)(i + 10)).ToArray();
        item.NameBytes = name; item.X = 64; item.Y = 96;
        int index = edit.Add(item); Assert.Equal(0, index); Assert.True(edit.IsDirty);
        Assert.Equal(item.RawData, layer.Objects[0].RawData); Assert.NotSame(item, layer.Objects[0]);
        item.X = 123; Assert.Equal(64, layer.Objects[0].X);
        edit.Undo(); Assert.Empty(layer.Objects); Assert.False(edit.IsDirty);
        edit.Redo(); Assert.Single(layer.Objects); Assert.Equal(name, layer.Objects[0].NameBytes);
        Assert.All(layer.Objects[0].RawData[48..], value => Assert.Equal(0, value));
        File.WriteAllBytes(fixture.Map.LyrPath, scene.SerializeLayers());
        var reopened = SceneDocument.Load(fixture.Map);
        Assert.Single(reopened.Layers[0].Objects); Assert.Equal(layer.Objects[0].RawData, reopened.Layers[0].Objects[0].RawData);
        edit.MarkSaved(); Assert.False(edit.IsDirty);
    }

    [Fact]
    public void Invalid_add_does_not_change_records_or_history()
    {
        var layer = new SceneLayer(); layer.Objects.Add(new SceneObject(new byte[56]));
        var edit = new SceneObjectEditService(layer);
        Assert.Throws<ArgumentNullException>(() => edit.Add(null!));
        Assert.Throws<ArgumentException>(() => edit.Add(new SceneObject(new byte[48])));
        Assert.Single(layer.Objects); Assert.False(edit.CanUndo); Assert.False(edit.IsDirty);
    }
}
