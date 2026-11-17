using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace TricksterMap.Scene
{
    /// <summary>Validates one resource envelope and retains its original bytes for no-op saves.</summary>
    public sealed class SceneEnvelope
    {
        private byte[] original;
        private uint magic;
        private byte[] payloadHash;
        public byte[] Payload { get; private set; }
        public bool IsCompressed { get; private set; }

        public static SceneEnvelope Decode(byte[] raw, uint expectedMagic)
        {
            if (raw == null || raw.Length > SceneBinary.Limit) throw new InvalidDataException("Scene resource exceeds supported size.");
            var result = new SceneEnvelope { original = (byte[])raw.Clone(), magic = expectedMagic };
            uint signature = raw.Length >= 4 ? BitConverter.ToUInt32(raw, 0) : 0;
            if (signature != expectedMagic)
            {
                if (signature == 0xa0b0 || signature == 0xc0d0 || signature == 0xe0f0)
                    throw new InvalidDataException("Scene companion compression signature mismatch.");
                result.Payload = (byte[])raw.Clone(); result.payloadHash = Hash(result.Payload); return result;
            }
            SceneBinary.Require(raw, 0, 12, "compression header");
            int length = SceneBinary.Int(raw, 4), packed = SceneBinary.Int(raw, 8);
            if (length < 0 || length > SceneBinary.Limit || packed < 0 || packed != raw.Length - 12)
                throw new InvalidDataException("Scene compressed resource length is invalid.");
            if (packed < 6 || (raw[12] & 15) != 8 || (raw[12] >> 4) > 7 ||
                ((raw[12] << 8) + raw[13]) % 31 != 0 || (raw[13] & 32) != 0)
                throw new InvalidDataException("Unsupported zlib resource header.");
            // Ionic.Zlib 1.9.1.5's streaming inflater reports incorrect Adler
            // checksums for some valid shipped resources. Decode raw DEFLATE
            // through the platform stream and independently check RFC1950 Adler.
            using (var input = new MemoryStream(raw, 14, packed - 6, false))
            using (var decoder = new System.IO.Compression.DeflateStream(input, System.IO.Compression.CompressionMode.Decompress, true))
            using (var output = new MemoryStream())
            {
                var buffer = new byte[81920]; int read;
                while ((read = decoder.Read(buffer, 0, Math.Min(buffer.Length, length - (int)output.Length + 1))) != 0)
                {
                    if (output.Length + read > length) throw new InvalidDataException("Scene decompression exceeds declared size.");
                    output.Write(buffer, 0, read);
                }
                if (output.Length != length) throw new InvalidDataException("Scene decompression length mismatch.");
                result.Payload = output.ToArray();
            }
            int checksumAt = raw.Length - 4;
            uint expected = (uint)raw[checksumAt] << 24 | (uint)raw[checksumAt + 1] << 16 |
                (uint)raw[checksumAt + 2] << 8 | raw[checksumAt + 3];
            if (Adler(result.Payload) != expected) throw new InvalidDataException("Scene resource checksum mismatch.");
            result.payloadHash = Hash(result.Payload); result.IsCompressed = true; return result;
        }

        public static byte[] Unpack(byte[] raw, uint expectedMagic) => Decode(raw, expectedMagic).Payload;

        public static SceneEnvelope Create(byte[] payload, uint magic, bool compressed)
        {
            if (payload == null || payload.Length > SceneBinary.Limit) throw new InvalidDataException("Scene resource exceeds supported size.");
            if (!compressed) return Decode(payload, magic);
            byte[] packed = CompressZlib(payload);
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(magic); writer.Write(payload.Length); writer.Write(packed.Length); writer.Write(packed);
                return Decode(stream.ToArray(), magic);
            }
        }

        public byte[] Serialize(byte[] payload)
        {
            if (payload == null || payload.Length > SceneBinary.Limit) throw new InvalidDataException("Scene resource exceeds supported size.");
            if (payloadHash.SequenceEqual(Hash(payload))) return (byte[])original.Clone();
            if (!IsCompressed) return (byte[])payload.Clone();
            byte[] packed = CompressZlib(payload);
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(magic); writer.Write(payload.Length); writer.Write(packed.Length); writer.Write(packed);
                return stream.ToArray();
            }
        }

        private static uint Adler(byte[] bytes)
        {
            uint a = 1, b = 0;
            for (int start = 0; start < bytes.Length; start += 5552)
            {
                int end = Math.Min(bytes.Length, start + 5552);
                for (int i = start; i < end; i++) { a += bytes[i]; b += a; }
                a %= 65521; b %= 65521;
            }
            return b << 16 | a;
        }
        private static byte[] CompressZlib(byte[] payload)
        {
            using (var stream = new MemoryStream())
            {
                // RFC1950: DEFLATE, 32K window, no dictionary, level hint 2.
                stream.WriteByte(0x78); stream.WriteByte(0x9c);
                using (var encoder = new System.IO.Compression.DeflateStream(stream,
                    System.IO.Compression.CompressionLevel.Optimal, true))
                    encoder.Write(payload, 0, payload.Length);
                uint checksum = Adler(payload);
                stream.WriteByte((byte)(checksum >> 24)); stream.WriteByte((byte)(checksum >> 16));
                stream.WriteByte((byte)(checksum >> 8)); stream.WriteByte((byte)checksum);
                return stream.ToArray();
            }
        }
        private static byte[] Hash(byte[] bytes) { using (var sha = SHA256.Create()) return sha.ComputeHash(bytes); }
    }
}
