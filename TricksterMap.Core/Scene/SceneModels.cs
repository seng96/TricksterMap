using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TricksterMap.Scene
{
    public sealed class SceneLayer
    {
        public byte[] NameBytes { get; set; } = new byte[32];
        public string Name => SceneBinary.Name(NameBytes);
        public int Kind { get; set; }
        public int Order { get; set; }
        public int ScaleX { get; set; } = 100;
        public int ScaleY { get; set; } = 100;
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public byte[] Payload { get; set; } = new byte[0];
        public List<SceneObject> Objects { get; } = new List<SceneObject>();
        public byte[] TrailingData { get; set; } = new byte[0];

        public uint GetTile(int x, int y) => BitConverter.ToUInt32(Payload, TileOffset(x, y));
        public void SetTile(int x, int y, uint value)
        {
            int offset = TileOffset(x, y);
            byte[] bytes = BitConverter.GetBytes(value);
            Buffer.BlockCopy(bytes, 0, Payload, offset, 4);
        }
        private int TileOffset(int x, int y)
        {
            if (Kind != 2 || x < 0 || y < 0 || x >= Width || y >= Height ||
                Payload == null || Payload.LongLength != (long)Width * Height * 4)
                throw new ArgumentOutOfRangeException("Tile coordinates or layer payload are invalid.");
            return checked((y * Width + x) * 4);
        }
    }

    public sealed class SceneObject
    {
        public SceneObject(byte[] rawData)
        {
            if (rawData == null || (rawData.Length != 48 && rawData.Length != 56))
                throw new ArgumentException("Scene object must retain a 48 or 56 byte record.");
            RawData = (byte[])rawData.Clone();
        }
        public byte[] RawData { get; }
        public int Flags { get => Read(0); set => Write(0, value); }
        public int X { get => Read(4); set => Write(4, value); }
        public int Y { get => Read(8); set => Write(8, value); }
        public int Z { get => Read(12); set => Write(12, value); }
        public byte[] NameBytes
        {
            get { var bytes = new byte[32]; Buffer.BlockCopy(RawData, 16, bytes, 0, 32); return bytes; }
            set
            {
                if (value == null || value.Length != 32) throw new ArgumentException("Scene name must retain 32 bytes.");
                Buffer.BlockCopy(value, 0, RawData, 16, 32);
            }
        }
        public string Name => SceneBinary.Name(NameBytes);
        private int Read(int at) => BitConverter.ToInt32(RawData, at);
        private void Write(int at, int value) { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, RawData, at, 4); }
    }

    public sealed class SceneTileGroup
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public byte[] Pixels { get; set; } = new byte[0];
        public byte[] TrailingData { get; set; } = new byte[0];
    }

    internal static class SceneBinary
    {
        internal const int Limit = 512 * 1024 * 1024;
        internal static void Require(byte[] data, long at, long length, string section)
        {
            if (data == null || at < 0 || length < 0 || at + length > data.LongLength)
                throw new InvalidDataException("Invalid scene " + section + " bounds.");
        }
        internal static int Int(byte[] data, int at) { Require(data, at, 4, "integer"); return BitConverter.ToInt32(data, at); }
        internal static byte[] Slice(byte[] data, int at, int length)
        {
            Require(data, at, length, "bytes"); var result = new byte[length];
            Buffer.BlockCopy(data, at, result, 0, length); return result;
        }
        internal static string Name(byte[] data)
        {
#if NET10_0_OR_GREATER
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
#endif
            int end = Array.IndexOf(data, (byte)0); if (end < 0) end = data.Length;
            return Encoding.GetEncoding(949).GetString(data, 0, end);
        }
        internal static void NameBytes(BinaryWriter writer, byte[] name)
        {
            if (name == null || name.Length != 32) throw new InvalidDataException("Scene names must contain exactly 32 bytes.");
            writer.Write(name);
        }
    }
}
