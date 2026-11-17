using TricksterMap.Data;

namespace TricksterMap.Tests.Editing;

public sealed class ConfigBrushTests
{
    private static ConfigLayer Layer(int width = 9, int height = 7) => new()
    {
        Type = 1, X = width, Y = height, BppX = 16, BppY = 16,
        Data = Enumerable.Repeat((byte)3, width * height).ToArray()
    };

    [Fact]
    public void Square_clips_at_top_left_without_wrapping_and_undo_restores_unknown_values()
    {
        var layer = Layer(); var edits = new ConfigLayerEditService(layer);
        edits.PaintBrush(0, 0, 5, 1, false);
        Assert.Equal(9, layer.Data.Count(v => v == 1));
        for (int y = 0; y < layer.Y; y++)
            for (int x = 0; x < layer.X; x++)
                Assert.Equal(x <= 2 && y <= 2 ? (byte)1 : (byte)3, layer.Data[y * layer.X + x]);
        edits.Undo(); Assert.All(layer.Data, value => Assert.Equal(3, value));
        Assert.False(edits.IsDirty);
    }

    [Fact]
    public void Circle_uses_cell_centers_and_differs_from_square_corners()
    {
        var layer = Layer(); var edits = new ConfigLayerEditService(layer);
        edits.PaintBrush(4, 3, 5, 0, true);
        Assert.Equal(21, layer.Data.Count(v => v == 0));
        Assert.Equal(3, layer.Data[1 * 9 + 2]);
        Assert.Equal(0, layer.Data[3 * 9 + 2]);
        edits.Undo(); Assert.All(layer.Data, value => Assert.Equal(3, value));
    }

    [Theory]
    [InlineData(1, 1)] [InlineData(2, 4)] [InlineData(3, 9)] [InlineData(4, 16)]
    public void Even_and_odd_square_diameters_have_exact_cell_count(int size, int count)
    {
        var layer = Layer(); var edits = new ConfigLayerEditService(layer);
        edits.PaintBrush(4, 3, size, 1, false);
        Assert.Equal(count, layer.Data.Count(v => v == 1));
    }

    [Fact]
    public void Fast_drag_is_contiguous_and_entire_drag_is_one_undo_step()
    {
        var layer = Layer(20, 20); var edits = new ConfigLayerEditService(layer);
        edits.BeginStroke();
        edits.DrawBrushLine(1, 1, 18, 18, 3, 0, false);
        edits.DrawBrushLine(18, 18, 18, 1, 5, 1, true);
        edits.EndStroke();
        byte[] painted = (byte[])layer.Data.Clone();
        for (int i = 1; i <= 15; i++) Assert.Equal(0, layer.Data[i * 20 + i]);
        edits.Undo(); Assert.All(layer.Data, value => Assert.Equal(3, value));
        Assert.False(edits.CanUndo);
        edits.Redo(); Assert.Equal(painted, layer.Data);
    }

    [Theory]
    [InlineData(0)] [InlineData(129)] [InlineData(int.MaxValue)]
    public void Invalid_diameter_rejects_without_mutation_or_history(int size)
    {
        var layer = Layer(); var edits = new ConfigLayerEditService(layer);
        Assert.Throws<ArgumentOutOfRangeException>(() => edits.DrawBrushLine(0, 0, 8, 6, size, 1, false));
        Assert.All(layer.Data, value => Assert.Equal(3, value));
        Assert.False(edits.CanUndo);
    }
}
