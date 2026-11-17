using TricksterMap.Scene;

namespace TricksterMap.Tests.Scene;

public class SceneTileEditTests
{
    [Fact]
    public void Stroke_preserves_unknown_flags_and_undo_restores_the_entire_original_payload()
    {
        using var fixture = new SceneFixture(); var scene = SceneDocument.Load(fixture.Map); var layer = scene.Layers[0];
        byte[] original = layer.Payload.ToArray(); var edit = new SceneTileEditService(scene, layer);
        edit.BeginStroke(); edit.SetCell(0, 0, 0x30000000); edit.SetCell(1, 1, 0x01000000); edit.EndStroke();
        Assert.True(edit.IsDirty); Assert.True(edit.CanUndo);
        edit.Undo(); Assert.Equal(original, layer.Payload); Assert.False(edit.IsDirty);
        edit.Redo(); Assert.Equal(0x30000000u, layer.GetTile(0, 0)); Assert.Equal(0x01000000u, layer.GetTile(1, 1));
        edit.MarkSaved(); Assert.False(edit.IsDirty); edit.Undo(); Assert.True(edit.IsDirty);
        edit.SetCell(0, 0, 0xffff); Assert.False(edit.CanRedo);
    }

    [Fact]
    public void Invalid_group_index_and_rectangle_are_rejected_before_modification()
    {
        using var fixture = new SceneFixture(); var scene = SceneDocument.Load(fixture.Map); var layer = scene.Layers[0];
        byte[] original = layer.Payload.ToArray(); var edit = new SceneTileEditService(scene, layer);
        Assert.Throws<ArgumentOutOfRangeException>(() => edit.SetCell(0, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => edit.SetCell(0, 0, 0x10000));
        Assert.Throws<ArgumentOutOfRangeException>(() => edit.FillRectangle(0, 0, 2, 1, 0));
        Assert.Throws<ArgumentException>(() => edit.FillRectangle(1, 0, 0, 1, 0));
        Assert.Equal(original, layer.Payload); Assert.False(edit.CanUndo);
    }
}
