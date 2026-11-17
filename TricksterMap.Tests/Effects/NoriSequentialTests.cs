using System.Text;
using TricksterMap.Scene;

namespace TricksterMap.Tests.Effects;

public class NoriSequentialTests
{
    private static byte[] Stream()
    {
        using var memory = new MemoryStream(); using var writer = new BinaryWriter(memory);
        writer.Write(0); writer.Write(0);
        foreach (string name in new[] { "first", "second" })
        {
            var bytes = new byte[32]; Encoding.ASCII.GetBytes(name).CopyTo(bytes, 0); writer.Write(bytes); writer.Write(1); writer.Write(0);
            writer.Write(50); writer.Write(1); writer.Write(0); writer.Write(new byte[24]); writer.Write(new byte[348]);
        }
        return memory.ToArray();
    }
    [Fact]
    public void Zero_tables_recover_two_distinct_animations_with_versioned_trailers()
    {
        var bytes = Stream(); var before = (byte[])bytes.Clone();
        var result = NoriSequentialAnimations.Read(bytes, 0, 2, 302, 1, out var names);
        Assert.Equal(2, result.Count); Assert.Equal(new[] { "first", "second" }, names.Values.Select(n => Encoding.ASCII.GetString(n).TrimEnd('\0')));
        Assert.Equal(before, bytes);
    }
    [Fact]
    public void Partial_nonzero_offsets_truncation_extra_tail_and_bad_bitmap_are_rejected()
    {
        var original = Stream();
        var nonzero = (byte[])original.Clone(); BitConverter.GetBytes(1).CopyTo(nonzero, 4);
        var badBitmap = (byte[])original.Clone(); BitConverter.GetBytes(2).CopyTo(badBitmap, 56);
        foreach (var data in new[] { original.Take(original.Length - 1).ToArray(), original.Concat(new byte[1]).ToArray(), nonzero, badBitmap })
            Assert.Throws<InvalidDataException>(() => NoriSequentialAnimations.Read(data, 0, 2, 302, 1, out _));
    }
}
