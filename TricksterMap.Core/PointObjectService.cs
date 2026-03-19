using System;
using TricksterMap.Data;

namespace TricksterMap
{
    public sealed class PointObjectService
    {
        public PointObject Add(MapDataInfo map, PointObject point)
        {
            NormalizeAndValidate(map, point, -1);
            map.PointObjects.Add(point);
            return point;
        }

        public PointObject Add(MapDocument document, PointObject point)
        {
            return Add(document.Map, point);
        }

        public PointObject Update(MapDataInfo map, int index, PointObject point)
        {
            EnsureIndex(map.PointObjects.Count, index, "point");
            NormalizeAndValidate(map, point, index);
            map.PointObjects[index] = point;
            return point;
        }

        public PointObject Update(MapDocument document, int index, PointObject point)
        {
            return Update(document.Map, index, point);
        }

        public void Delete(MapDataInfo map, int index)
        {
            EnsureIndex(map.PointObjects.Count, index, "point");
            map.PointObjects.RemoveAt(index);
        }

        public void Delete(MapDocument document, int index)
        {
            Delete(document.Map, index);
        }

        private static void NormalizeAndValidate(MapDataInfo map, PointObject point, int currentIndex)
        {
            if (Array.IndexOf(PointObject.ValidTypes, point.Type) < 0)
            {
                throw new InvalidOperationException(string.Format("Unsupported point type: {0}", point.Type));
            }

            if (point.Type == 0x01 || point.Type == 0x02)
            {
                point.Id = 0;
                return;
            }

            for (var i = 0; i < map.PointObjects.Count; i++)
            {
                if (i == currentIndex)
                {
                    continue;
                }

                var existing = map.PointObjects[i];
                if (existing.Type == point.Type && existing.Id == point.Id)
                {
                    throw new InvalidOperationException("Duplicate ID found.");
                }
            }
        }

        private static void EnsureIndex(int count, int index, string objectType)
        {
            if (index < 0 || index >= count)
            {
                throw new InvalidOperationException(
                    string.Format("The specified {0} index does not exist: {1}", objectType, index));
            }
        }
    }
}
