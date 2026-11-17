using TricksterMap.Scene;

namespace TricksterMap.Tests.Scene;

public class SceneTilePixelCodecTests
{
    // An asymmetric raw TIL fixture: red/green on row zero, blue/white on row one.
    // Expected screen corners are independent of both encoder and decoder.
    private static readonly byte[] Tile = { 0, 0x7c, 0xe0, 3, 0x1f, 0, 0xff, 0x7f };
    private static readonly uint[] Colors = { 0xffff0000, 0xff00ff00, 0xff0000ff, 0xffffffff };
    [Theory]
    [InlineData(0, 0, 1, 2, 3)]
    [InlineData(1, 1, 0, 3, 2)]
    [InlineData(2, 2, 3, 0, 1)]
    [InlineData(3, 3, 2, 1, 0)]
    public void Raw_tile_row_zero_is_the_top_and_flags_flip_only_when_set(byte flags, int a, int b, int c, int d)
    {
        byte[] decoded = SceneTilePixelCodec.Decode(Tile, 0, 2, 2, flags);
        Assert.Equal(new[] { Colors[a], Colors[b], Colors[c], Colors[d] },
            Enumerable.Range(0, 4).Select(i => BitConverter.ToUInt32(decoded, i * 4)).ToArray());
    }
    [Fact]
    public void Atlas_encoding_uses_native_top_down_rows_and_tile_major_group_order()
    {
        // Two side-by-side tiles: red/green;blue/white then white/blue;green/red.
        uint[] atlas = { Colors[0], Colors[1], Colors[3], Colors[2], Colors[2], Colors[3], Colors[1], Colors[0] };
        byte[] bgra = atlas.SelectMany(BitConverter.GetBytes).ToArray();
        byte[] encoded = SceneTilePixelCodec.EncodeAtlas(bgra, 4, 2, 2, 2);
        Assert.Equal(new byte[] { 0, 0x7c, 0xe0, 3, 0x1f, 0, 0xff, 0x7f, 0xff, 0x7f, 0x1f, 0, 0xe0, 3, 0, 0x7c }, encoded);
        Assert.Equal(bgra, Enumerable.Range(0, 2).SelectMany(row =>
            Enumerable.Range(0, 2).SelectMany(tile => SceneTilePixelCodec.Decode(encoded, tile * 8, 2, 2).Skip(row * 8).Take(8))).ToArray());
    }
    [Fact]
    public void Native_magenta_key_is_transparent_and_invalid_bounds_are_rejected()
    {
        Assert.Equal(0, SceneTilePixelCodec.Decode(new byte[] { 0x1f, 0x7c }, 0, 1, 1)[3]);
        Assert.Throws<InvalidDataException>(() => SceneTilePixelCodec.Decode(Tile, 2, 2, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => SceneTilePixelCodec.Decode(Tile, 0, 0, 2));
        Assert.Throws<ArgumentException>(() => SceneTilePixelCodec.EncodeAtlas(new byte[3], 1, 1, 1, 1));
    }
}
