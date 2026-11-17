namespace TricksterMap.Tests.Editing;

public sealed class ViewportTransformTests
{
    [Fact]
    public void Fit_preserves_aspect_ratio_and_centers_map_inside_padding()
    {
        var view = new ViewportTransform();
        view.Fit(2272, 1760, 680, 360);
        Assert.Equal(320.0 / 1760, view.Zoom, 12);
        Assert.Equal(340, view.ScreenX(2272 / 2.0), 10);
        Assert.Equal(180, view.ScreenY(1760 / 2.0), 10);
        Assert.True(view.ScreenX(0) >= 20);
        Assert.Equal(20, view.ScreenY(0), 10);
        Assert.Equal(340, view.ScreenY(1760), 10);
    }

    [Theory]
    [InlineData(0.0001)]
    [InlineData(0.05)]
    [InlineData(0.8)]
    [InlineData(200)]
    public void Zoom_keeps_arbitrary_cursor_world_point_fixed_even_at_limits(double zoom)
    {
        var view = new ViewportTransform();
        view.Fit(10240, 6400, 900, 700); view.Pan(-51.25, 87.75);
        double worldX = view.WorldX(357.125), worldY = view.WorldY(211.75);
        view.ZoomAt(357.125, 211.75, zoom);
        Assert.Equal(worldX, view.WorldX(357.125), 8);
        Assert.Equal(worldY, view.WorldY(211.75), 8);
        Assert.InRange(view.Zoom, ViewportTransform.MinimumZoom, ViewportTransform.MaximumZoom);
    }

    [Fact]
    public void Pan_is_in_screen_pixels_and_does_not_change_zoom()
    {
        var view = new ViewportTransform();
        view.ZoomAt(0, 0, .375);
        double before = view.WorldX(0);
        view.Pan(31.5, -12.75);
        Assert.Equal(.375, view.Zoom);
        Assert.Equal(before - 31.5 / .375, view.WorldX(0), 9);
        Assert.Equal(-12.75, view.ScreenY(0));
    }

    [Fact]
    public void Transform_round_trips_subpixel_points_after_repeated_wheel_zoom()
    {
        var view = new ViewportTransform();
        view.Fit(2272, 1760, 668, 400);
        for (int i = 0; i < 40; i++) view.ZoomAt(370.125, 125.625, view.Zoom * 1.2);
        Assert.Equal(123.456, view.WorldX(view.ScreenX(123.456)), 9);
        Assert.Equal(-17.625, view.WorldY(view.ScreenY(-17.625)), 9);
    }

    [Fact]
    public void Reset_centers_map_at_one_world_pixel_per_view_pixel()
    {
        var view = new ViewportTransform();
        view.Fit(2272, 1760, 680, 360); view.Pan(123, 456);
        view.Reset(2272, 1760, 680, 360);
        Assert.Equal(1, view.Zoom);
        Assert.Equal(340, view.ScreenX(1136));
        Assert.Equal(180, view.ScreenY(880));
    }

    [Theory]
    [InlineData(0)] [InlineData(-1)] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)]
    public void Invalid_zoom_is_rejected_without_moving_view(double zoom)
    {
        var view = new ViewportTransform();
        view.Pan(3, 4);
        Assert.Throws<ArgumentOutOfRangeException>(() => view.ZoomAt(100, 100, zoom));
        Assert.Equal(1, view.Zoom); Assert.Equal(3, view.OriginX); Assert.Equal(4, view.OriginY);
    }
}
