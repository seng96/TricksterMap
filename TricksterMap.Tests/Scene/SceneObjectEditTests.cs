using TricksterMap.Scene;

namespace TricksterMap.Tests.Scene;

public class SceneObjectEditTests
{
    [Fact]
    public void Object_transform_and_flags_keep_name_and_unknown_tail_and_restore_exact_record()
    {
        using var fixture = new SceneFixture(); var scene = SceneDocument.Load(fixture.Map); var layer = scene.Layers[0];
        byte[] original = layer.Objects[0].RawData.ToArray(); var edit = new SceneObjectEditService(layer);
        edit.SetPosition(0, -20, 120, 30);
        Assert.Equal(-20, layer.Objects[0].X); Assert.Equal(120, layer.Objects[0].Y); Assert.Equal(30, layer.Objects[0].Z);
        Assert.Equal(original[16..], layer.Objects[0].RawData[16..]);
        edit.SetFlags(0, 17); edit.Undo(); Assert.Equal(original[..4], layer.Objects[0].RawData[..4]);
        edit.Undo(); Assert.Equal(original, layer.Objects[0].RawData); Assert.False(edit.IsDirty);
        edit.Redo(); Assert.Equal(-20, layer.Objects[0].X); edit.MarkSaved(); Assert.False(edit.IsDirty);
        Assert.Throws<ArgumentOutOfRangeException>(() => edit.SetPosition(1, 1, 2, 3));
    }

    [Fact]
    public void Duplicate_delete_transform_history_restores_exact_records_and_exported_object_count()
    {
        using var fixture = new SceneFixture(303, true); var scene = SceneDocument.Load(fixture.Map); var layer = scene.Layers[0];
        byte[] original = layer.Objects[0].RawData.ToArray(); var edit = new SceneObjectEditService(layer);
        int duplicate = edit.Duplicate(0); Assert.Equal(1, duplicate); Assert.Equal(2, layer.Objects.Count);
        Assert.Equal(original, layer.Objects[1].RawData); Assert.NotSame(layer.Objects[0], layer.Objects[1]);
        edit.SetPosition(1, 123, 456, 78); edit.Delete(0); Assert.Single(layer.Objects); Assert.Equal(123, layer.Objects[0].X);
        edit.Undo(); Assert.Equal(2, layer.Objects.Count); Assert.Equal(original, layer.Objects[0].RawData);
        edit.Undo(); Assert.Equal(original, layer.Objects[1].RawData);
        edit.Undo(); Assert.Single(layer.Objects); Assert.Equal(original, layer.Objects[0].RawData); Assert.False(edit.IsDirty);
        edit.Redo(); edit.Redo(); edit.Redo(); Assert.Single(layer.Objects); Assert.Equal(123, layer.Objects[0].X);
        File.WriteAllBytes(fixture.Map.LyrPath, scene.SerializeLayers());
        var reopened = SceneDocument.Load(fixture.Map); Assert.Single(reopened.Layers[0].Objects);
        Assert.Equal(123, reopened.Layers[0].Objects[0].X); Assert.Equal(original[16..], reopened.Layers[0].Objects[0].RawData[16..]);
        edit.Delete(0); Assert.Empty(layer.Objects); edit.Undo(); Assert.Single(layer.Objects);
    }

    [Fact]
    public void Invalid_structural_index_changes_no_records_or_history()
    {
        using var fixture = new SceneFixture(); var scene = SceneDocument.Load(fixture.Map); var layer = scene.Layers[0];
        byte[] original = layer.Objects[0].RawData.ToArray(); var edit = new SceneObjectEditService(layer);
        Assert.Throws<ArgumentOutOfRangeException>(() => edit.Duplicate(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => edit.Delete(-1));
        Assert.Single(layer.Objects); Assert.Equal(original, layer.Objects[0].RawData); Assert.False(edit.CanUndo);
    }
}
