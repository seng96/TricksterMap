using System;
using TricksterMap.Data;

namespace TricksterMap
{
    public sealed class RangeObjectService
    {
        public RangeObject Add(MapDataInfo map, RangeObject range)
        {
            Validate(range);
            map.RangeObjects.Add(range);
            return range;
        }

        public RangeObject Add(MapDocument document, RangeObject range)
        {
            return Add(document.Map, range);
        }

        public RangeObject Update(MapDataInfo map, int index, RangeObject range)
        {
            EnsureIndex(map.RangeObjects.Count, index);
            Validate(range);
            map.RangeObjects[index] = range;
            return range;
        }

        public RangeObject Update(MapDocument document, int index, RangeObject range)
        {
            return Update(document.Map, index, range);
        }

        public void Delete(MapDataInfo map, int index)
        {
            EnsureIndex(map.RangeObjects.Count, index);
            map.RangeObjects.RemoveAt(index);
        }

        public void Delete(MapDocument document, int index)
        {
            Delete(document.Map, index);
        }

        private static void Validate(RangeObject range)
        {
            if (Array.IndexOf(RangeObject.ValidTypes, range.Type) < 0)
            {
                throw new InvalidOperationException(string.Format("Unsupported range type: {0}", range.Type));
            }
        }

        private static void EnsureIndex(int count, int index)
        {
            if (index < 0 || index >= count)
            {
                throw new InvalidOperationException(
                    string.Format("The specified range index does not exist: {0}", index));
            }
        }
    }
}
