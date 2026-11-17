using System.IO;

namespace TricksterMap
{
    internal static class MapBinaryGuard
    {
        internal const int MaxDocumentBytes = 512 * 1024 * 1024;

        internal static void Require(BinaryReader reader, long bytes, string section)
        {
            if (bytes < 0 || bytes > MaxDocumentBytes ||
                bytes > reader.BaseStream.Length - reader.BaseStream.Position)
                throw new InvalidDataException("Truncated or oversized MDN " + section + ".");
        }

        internal static void Count(BinaryReader reader, int value, int stride, string section)
        {
            if (value < 0 || (long)value * stride > MaxDocumentBytes)
                throw new InvalidDataException("Invalid MDN " + section + " count.");
            Require(reader, (long)value * stride, section);
        }

        internal static int LayerLength(int x, int y)
        {
            long length = (long)x * y;
            if (x <= 0 || y <= 0 || length > MaxDocumentBytes)
                throw new InvalidDataException("Invalid MDN configuration layer dimensions.");
            return (int)length;
        }

        internal static byte[] Read(BinaryReader reader, int bytes, string section)
        {
            Require(reader, bytes, section);
            var result = reader.ReadBytes(bytes);
            if (result.Length != bytes)
                throw new InvalidDataException("Truncated MDN " + section + ".");
            return result;
        }
    }
}
