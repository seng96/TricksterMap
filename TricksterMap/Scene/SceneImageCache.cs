using System;
using System.Collections.Generic;
using System.Drawing;

namespace TricksterMap.SceneView
{
    /// <summary>Owns decoded images and limits retained RGBA storage per scene.</summary>
    internal sealed class SceneImageCache : IDisposable
    {
        private readonly Dictionary<string, LinkedListNode<Entry>> entries = new Dictionary<string, LinkedListNode<Entry>>();
        private readonly LinkedList<Entry> age = new LinkedList<Entry>();
        private long bytes;
        private const long Capacity = 64L * 1024 * 1024;
        private sealed class Entry { internal string Key; internal Bitmap Image; internal long Cost; }

        internal Bitmap Get(string key, Func<Bitmap> create)
        {
            LinkedListNode<Entry> found;
            if (entries.TryGetValue(key, out found))
            { age.Remove(found); age.AddLast(found); return found.Value.Image; }
            Bitmap image = create();
            long cost = (long)image.Width * image.Height * 4;
            while (age.Count > 0 && (bytes + cost > Capacity || entries.Count >= 1024))
            {
                var first = age.First; age.RemoveFirst(); entries.Remove(first.Value.Key);
                bytes -= first.Value.Cost; first.Value.Image.Dispose();
            }
            var entry = new Entry { Key = key, Image = image, Cost = cost };
            entries.Add(key, age.AddLast(entry)); bytes += cost;
            return image;
        }

        public void Dispose()
        {
            foreach (var entry in age) entry.Image.Dispose();
            age.Clear(); entries.Clear(); bytes = 0;
        }
    }
}
