using System;
using System.Drawing;
using System.Linq;
using TricksterMap.Data;

namespace TricksterMap
{
    internal sealed partial class ViewportMapObjectOverlay
    {
        private static readonly RangeGrip[] Corners =
        {
            RangeGrip.X1 | RangeGrip.Y1, RangeGrip.X2 | RangeGrip.Y1,
            RangeGrip.X1 | RangeGrip.Y2, RangeGrip.X2 | RangeGrip.Y2
        };
        private readonly MapDataInfo map;
        private readonly ViewportTransform view;
        private readonly ViewportMapObjectPositions history;
        private object selected, dragging;
        private Point start;
        private MapObjectPosition initial, pending;
        private RangeGrip grip;
        public bool ShowPoints { get; set; }
        public bool ShowRanges { get; set; }
        public MapObjectTool Tool { get; set; }
        public bool Dragging => dragging != null;
        public bool CanUndo => history.CanUndo;
        public bool CanRedo => history.CanRedo;
        private bool Visible(object item) => item is PointObject ? ShowPoints : item is RangeObject && ShowRanges;

        public ViewportMapObjectOverlay(MapDataInfo map, ViewportTransform view)
        {
            this.map = map; this.view = view;
            history = new ViewportMapObjectPositions(map);
        }

        public bool Begin(Point pointer)
        {
            grip = RangeGrip.Move;
            if (Tool == MapObjectTool.Point && ShowPoints)
                selected = PickPoint(pointer);
            else if (Tool == MapObjectTool.Range && ShowRanges)
                selected = PickRange(pointer);
            else selected = null;
            dragging = selected;
            if (dragging == null) return false;
            initial = pending = ViewportMapObjectPositions.Read(dragging);
            start = pointer;
            return true;
        }

        private RangeObject PickRange(Point pointer)
        {
            if (selected is RangeObject range && history.Contains(range))
            {
                var pos = ViewportMapObjectPositions.Read(range);
                foreach (var corner in Corners)
                    if (Near(pointer, Corner(pos, corner), 7))
                    { grip = corner; return range; }
            }
            return ClosestRange(pointer);
        }

        private PointObject PickPoint(Point pointer)
            => PointsAt(pointer).LastOrDefault();

        public PointObject[] PointsAt(Point pointer) => ShowPoints ?
            map.PointObjects.Select(p => new { Item = p, Distance = DistanceSquared(pointer, Screen(p.X, p.Y)) })
                .Where(hit => hit.Distance <= 144).OrderByDescending(hit => hit.Distance)
                .Select(hit => hit.Item).ToArray() : Array.Empty<PointObject>();

        private static double DistanceSquared(Point a, PointF b)
        { double x = a.X-b.X, y = a.Y-b.Y; return x*x+y*y; }

        private RangeObject ClosestRange(Point pointer)
            => RangesAt(pointer).LastOrDefault();

        public RangeObject[] RangesAt(Point pointer) => ShowRanges ?
            map.RangeObjects.Where(obj => NearRange(pointer, Bounds(ViewportMapObjectPositions.Read(obj))))
                .OrderByDescending(obj => Math.Abs((double)obj.X2 - obj.X1) * Math.Abs((double)obj.Y2 - obj.Y1))
                .ToArray() : Array.Empty<RangeObject>();

        public string MenuCaption(object item) => item is PointObject p ?
            $"點位 #{map.PointObjects.IndexOf(p) + 1} · {p.GetTypeName()} · ID {p.Id}" :
            RangeMenuCaption((RangeObject)item);

        private string RangeMenuCaption(RangeObject r)
            => $"範圍 #{map.RangeObjects.IndexOf(r) + 1} · {r.GetTypeName()} · ID {r.Id}";

        private static bool NearRange(Point point, RectangleF box)
        { box.Inflate(4, 4); return box.Contains(point); }

        public void Move(Point pointer)
        {
            if (dragging == null) return;
            long x = (long)Math.Round((pointer.X - (double)start.X) / view.Zoom, MidpointRounding.AwayFromZero);
            long y = (long)Math.Round((pointer.Y - (double)start.Y) / view.Zoom, MidpointRounding.AwayFromZero);
            pending = initial.Offset(x, y, grip);
        }
        public bool Commit(Point pointer)
        {
            if (dragging == null) return false;
            Move(pointer);
            var target = dragging;
            dragging = null;
            return history.Move(target, pending);
        }
        public void Cancel() { dragging = null; hovered = null; }
        public bool ChangeHistory(bool reverse)
        {
            Cancel();
            var changed = history.ChangeHistory(reverse);
            if (changed == null) return false;
            selected = changed;
            return true;
        }

        public bool RecordMetadataEdit(object original, object edited)
        {
            Cancel();
            bool changed = history.RecordReplacement(original, edited);
            selected = changed ? edited : original;
            return changed;
        }

        public string SelectionText
        {
            get
            {
                if (!Visible(selected) || !history.Contains(selected))
                    return Tool == MapObjectTool.Point ? "左鍵拖曳可見點位。" : "左鍵拖曳範圍，或拖曳選取框角點調整大小。";
                var p = Current(selected);
                if (selected is PointObject point)
                    return $"點位 #{map.PointObjects.IndexOf(point) + 1}  {point.GetTypeName()}  ID {point.Id}  ({p.X1}, {p.Y1})";
                var range = (RangeObject)selected;
                return $"範圍 #{map.RangeObjects.IndexOf(range) + 1}  {range.GetTypeName()}  ID {range.Id}  ({p.X1}, {p.Y1}) → ({p.X2}, {p.Y2})";
            }
        }

        private MapObjectPosition Current(object obj) => obj == dragging ? pending : ViewportMapObjectPositions.Read(obj);
        private PointF Screen(int x, int y) => new PointF((float)view.ScreenX(x), (float)view.ScreenY(y));
        private PointF Corner(MapObjectPosition pos, RangeGrip corner) =>
            Screen((corner & RangeGrip.X2) == 0 ? pos.X1 : pos.X2, (corner & RangeGrip.Y2) == 0 ? pos.Y1 : pos.Y2);
        internal RectangleF Bounds(MapObjectPosition pos)
        {
            var a = Screen(Math.Min(pos.X1,pos.X2), Math.Min(pos.Y1,pos.Y2));
            var b = Screen(Math.Max(pos.X1,pos.X2), Math.Max(pos.Y1,pos.Y2));
            return RectangleF.FromLTRB(a.X, a.Y, b.X, b.Y);
        }
        private static bool Near(Point p, PointF q, int radius)
            => Math.Abs(p.X-q.X)<=radius && Math.Abs(p.Y-q.Y)<=radius;
    }
}
