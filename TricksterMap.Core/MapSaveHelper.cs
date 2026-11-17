using System;
using System.IO;
using TricksterMap.Data;

namespace TricksterMap
{
    public class MapSaveHelper
    {
        public static byte[] Serialize(MapDataInfo map)
        {
            return MapSerializer.Serialize(map);
        }

        public static void Save(string filename, MapDataInfo map)
        {
            byte[] bytes = Serialize(map);
            string target = Path.GetFullPath(filename);
            string temporary = Path.Combine(Path.GetDirectoryName(target), "." + Path.GetFileName(target) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(target)) File.Replace(temporary, target, null);
                else File.Move(temporary, target);
                map.MapSizeSum = BitConverter.ToInt32(bytes, 20);
                map.OriginalSerializedLength = bytes.Length;
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }
}
