using System;
using System.Text;

namespace TricksterMap.Effects
{
    /// <summary>Read-only view; all opaque MDN effect bytes remain owned by MapDataInfo.</summary>
    public sealed class MapEffectInstance
    {
        public MapEffectInstance(byte[] record)
        {
            if (record == null || record.Length != 52) throw new ArgumentException("An MDN effect record must contain 52 bytes.");
            RawData = (byte[])record.Clone();
        }
        public byte[] RawData { get; }
        public int Id => BitConverter.ToInt32(RawData, 0);
        public int X => BitConverter.ToInt32(RawData, 4);
        public int Y => BitConverter.ToInt32(RawData, 8);
        public int LayerHint => BitConverter.ToInt32(RawData, 16);
        public string Name
        {
            get
            {
#if NET10_0_OR_GREATER
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
#endif
                int length = Array.IndexOf(RawData, (byte)0, 20, 32);
                return Encoding.GetEncoding(949, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback)
                    .GetString(RawData, 20, (length < 0 ? 52 : length) - 20);
            }
        }
    }
}
