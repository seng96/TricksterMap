using System;
using System.Collections.Generic;
using TricksterMap.Data;

namespace TricksterMap
{
    internal enum MapObjectTool { Collision, Point, Range }

    [Flags]
    internal enum RangeGrip { Move = 0, X1 = 1, Y1 = 2, X2 = 4, Y2 = 8 }

    internal readonly struct MapObjectPosition : IEquatable<MapObjectPosition>
    {
        public readonly int X1, Y1, X2, Y2;
        public MapObjectPosition(int x1, int y1, int x2, int y2)
        { X1 = x1; Y1 = y1; X2 = x2; Y2 = y2; }
        public bool Equals(MapObjectPosition other) => X1 == other.X1 && Y1 == other.Y1 && X2 == other.X2 && Y2 == other.Y2;

        public MapObjectPosition Offset(long dx, long dy, RangeGrip grip)
        {
            if (grip == RangeGrip.Move)
            {
                dx = Clamp(dx, int.MinValue - (long)Math.Min(X1, X2), int.MaxValue - (long)Math.Max(X1, X2));
                dy = Clamp(dy, int.MinValue - (long)Math.Min(Y1, Y2), int.MaxValue - (long)Math.Max(Y1, Y2));
                return new MapObjectPosition((int)(X1 + dx), (int)(Y1 + dy), (int)(X2 + dx), (int)(Y2 + dy));
            }
            int x1 = X1, x2 = X2, y1 = Y1, y2 = Y2;
            if ((grip & RangeGrip.X1) != 0) x1 = Endpoint(X1, X2, dx, X1 <= X2);
            if ((grip & RangeGrip.X2) != 0) x2 = Endpoint(X2, X1, dx, X2 < X1);
            if ((grip & RangeGrip.Y1) != 0) y1 = Endpoint(Y1, Y2, dy, Y1 <= Y2);
            if ((grip & RangeGrip.Y2) != 0) y2 = Endpoint(Y2, Y1, dy, Y2 < Y1);
            return new MapObjectPosition(x1, y1, x2, y2);
        }

        private static int Endpoint(int value, int other, long delta, bool low)
            => low ? (int)Clamp(value + delta, int.MinValue, other) : (int)Clamp(value + delta, other, int.MaxValue);
        private static long Clamp(long value, long lo, long hi) => Math.Max(lo, Math.Min(hi, value));
    }

    internal sealed class ViewportMapObjectPositions
    {
        private readonly MapDataInfo map;
        private readonly Stack<Edit> undo = new Stack<Edit>(), redo = new Stack<Edit>();
        private sealed class Edit
        {
            public object BeforeItem, AfterItem;
            public MapObjectPosition Before, After;
        }
        public ViewportMapObjectPositions(MapDataInfo map) { this.map = map; }
        public bool Contains(object item) => item is PointObject point ? map.PointObjects.Contains(point) :
            item is RangeObject range && map.RangeObjects.Contains(range);
        public static MapObjectPosition Read(object item)
        {
            if (item is PointObject point) return new MapObjectPosition(point.X, point.Y, point.X, point.Y);
            var range = (RangeObject)item;
            return new MapObjectPosition(range.X1, range.Y1, range.X2, range.Y2);
        }
        private static void Write(object item, MapObjectPosition value)
        {
            if (item is PointObject point) { point.X = value.X1; point.Y = value.Y1; return; }
            var range = (RangeObject)item;
            range.X1 = value.X1; range.Y1 = value.Y1; range.X2 = value.X2; range.Y2 = value.Y2;
        }
        public bool Move(object item, MapObjectPosition value)
        {
            if (!Contains(item)) return false;
            var before = Read(item);
            if (before.Equals(value)) return false;
            Write(item, value);
            undo.Push(new Edit { BeforeItem = item, AfterItem = item, Before = before, After = value });
            redo.Clear(); return true;
        }
        public bool RecordReplacement(object before, object after)
        {
            if (!Contains(after) || before == after) return false;
            var oldPosition = Read(before); var newPosition = Read(after);
            if (oldPosition.Equals(newPosition) && SameMetadata(before, after))
            {
                Replace(after, before);
                return false;
            }
            undo.Push(new Edit { BeforeItem = before, AfterItem = after, Before = oldPosition, After = newPosition });
            redo.Clear(); return true;
        }

        private static bool SameMetadata(object before, object after)
        {
            if (before is PointObject a && after is PointObject b)
                return a.Type == b.Type && a.Id == b.Id && a.MapId == b.MapId && a.Options == b.Options;
            var x = (RangeObject)before; var y = (RangeObject)after;
            return x.Type == y.Type && x.Id == y.Id && x.Destination == y.Destination && x.Options == y.Options;
        }

        private void Replace(object current, object replacement)
        {
            if (current is PointObject point)
                map.PointObjects[map.PointObjects.IndexOf(point)] = (PointObject)replacement;
            else
                map.RangeObjects[map.RangeObjects.IndexOf((RangeObject)current)] = (RangeObject)replacement;
        }

        // Rows replaced or deleted through the list editor invalidate the corresponding viewport history.
        private bool HasEdit(Stack<Edit> history, bool reverse)
        {
            while (history.Count > 0)
            {
                var entry = history.Peek();
                object item = reverse ? entry.AfterItem : entry.BeforeItem;
                if (Contains(item) && Read(item).Equals(reverse ? entry.After : entry.Before)) return true;
                history.Pop();
            }
            return false;
        }
        public bool CanUndo => HasEdit(undo, true);
        public bool CanRedo => HasEdit(redo, false);
        public object ChangeHistory(bool reverse)
        {
            var source = reverse ? undo : redo;
            if (!HasEdit(source, reverse)) return null;
            var edit = source.Pop();
            object current = reverse ? edit.AfterItem : edit.BeforeItem;
            object replacement = reverse ? edit.BeforeItem : edit.AfterItem;
            var position = reverse ? edit.Before : edit.After;
            if (current != replacement) Replace(current, replacement);
            Write(replacement, position);
            (reverse ? redo : undo).Push(edit);
            return replacement;
        }
    }
}
