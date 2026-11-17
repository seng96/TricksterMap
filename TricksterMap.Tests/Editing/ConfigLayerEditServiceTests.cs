using TricksterMap.Data;

namespace TricksterMap.Tests.Editing;

public class ConfigLayerEditServiceTests
{
    private static ConfigLayer Grid() => new() { Type = 1, X = 4, Y = 3, BppX = 16, BppY = 16,
        Data = new byte[] { 0, 1, 2, 3, 0, 1, 0, 1, 0, 0, 0, 0 } };

    [Fact]
    public void Stroke_coalesces_cells_and_restores_unknown_values()
    {
        var layer = Grid(); var original = (byte[])layer.Data.Clone();
        var edits = new ConfigLayerEditService(layer);
        edits.BeginStroke(); edits.SetCell(2, 0, 1); edits.SetCell(2, 0, 0); edits.SetCell(3, 0, 1); edits.EndStroke();
        Assert.True(edits.IsDirty);
        edits.Undo(); Assert.Equal(original, layer.Data); Assert.False(edits.IsDirty); Assert.False(edits.CanUndo);
        edits.Redo(); Assert.Equal(0, layer.Data[2]); Assert.Equal(1, layer.Data[3]);
        edits.MarkSaved(); Assert.False(edits.IsDirty);
        edits.Undo(); Assert.True(edits.IsDirty); edits.Redo(); Assert.False(edits.IsDirty);
    }

    [Fact]
    public void Rectangle_is_inclusive_and_does_not_touch_neighbours()
    {
        var layer = Grid(); var edits = new ConfigLayerEditService(layer);
        edits.FillRectangle(1, 1, 2, 2, 255);
        Assert.Equal(new byte[] { 0, 1, 2, 3, 0, 255, 255, 1, 0, 255, 255, 0 }, layer.Data);
        edits.Undo(); Assert.Equal(Grid().Data, layer.Data);
    }

    [Fact]
    public void Invalid_rectangle_has_no_partial_effect()
    {
        var layer = Grid(); var edits = new ConfigLayerEditService(layer);
        Assert.Throws<ArgumentOutOfRangeException>(() => edits.FillRectangle(0, 0, 4, 2, 1));
        Assert.Throws<ArgumentException>(() => edits.FillRectangle(3, 0, 1, 2, 1));
        Assert.Equal(Grid().Data, layer.Data); Assert.False(edits.CanUndo);
        Assert.Throws<ArgumentException>(() => new ConfigLayerEditService(new ConfigLayer { X = int.MaxValue, Y = 2, Data = new byte[2] }));
    }

    [Fact]
    public void Drag_line_interpolates_and_edit_after_undo_discards_redo()
    {
        var layer = Grid(); var edits = new ConfigLayerEditService(layer);
        edits.DrawLine(0, 2, 3, 2, 1);
        Assert.Equal(new byte[] { 1, 1, 1, 1 }, layer.Data[8..]);
        edits.Undo(); Assert.True(edits.CanRedo);
        edits.SetCell(0, 0, 1); Assert.False(edits.CanRedo);
    }
}
