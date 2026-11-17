using System;
using System.Text;

namespace TricksterMap.SceneView
{
    /// <summary>A unique existing BAC animation name with its original 32 bytes.</summary>
    public sealed class SceneObjectAsset
    {
        private readonly byte[] name;
        public string DisplayName { get; }
        public byte[] NameBytes => (byte[])name.Clone();
        internal SceneObjectAsset(byte[] rawName)
        {
            if (rawName == null || rawName.Length != 32) throw new ArgumentException("BAC name must contain 32 bytes.");
            name = (byte[])rawName.Clone(); int length = Array.IndexOf(name, (byte)0);
            if (length < 0) length = name.Length;
            DisplayName = length == 0 ? "（未命名素材）" : Encoding.GetEncoding(949).GetString(name, 0, length);
        }
        public override string ToString() => DisplayName;
    }
}
