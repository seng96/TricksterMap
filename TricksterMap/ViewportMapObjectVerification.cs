using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using TricksterMap.Data;

namespace TricksterMap
{
    internal sealed partial class CollisionCanvas
    {
        internal void VerifyPointAndRangeGestures()
        {
            var previousOverlay = ObjectOverlay;
            var fixture = new MapDataInfo();
            var point = new PointObject { Type = 0x7e, Id = 35, MapId = 153, Options = 90, X = 80, Y = 80 };
            var range = new RangeObject { Type = 0, Id = 9, Destination = 153, Options = 92, X1 = 90, Y1 = 110, X2 = 160, Y2 = 170 };
            fixture.PointObjects.Add(point); fixture.RangeObjects.Add(range);
            fixture.PointObjects.Add(new PointObject { Type = 0x04, Id = 21, X = 35, Y = 100 });
            fixture.PointObjects.Add(new PointObject { Type = 0x03, Id = 177, X = 230, Y = 145 });
            fixture.PointObjects.Add(new PointObject { Type = 0x01, MapId = 153, X = 120, Y = 140 });
            ObjectOverlay = new ViewportMapObjectOverlay(fixture, View) { Tool = MapObjectTool.Point };
            double zoom = View.Zoom, ox = View.OriginX, oy = View.OriginY;
            verifying = true;
            try
            {
                View.ZoomAt(0, 0, 1.5);
                View.Pan(20 - View.OriginX, 20 - View.OriginY);
                VerifyHoverDetails();
                VerifyRightClickActions(fixture);
                var start = new Point(140, 140); var moved = new Point(170, 158);
                TestGesture(start, moved, false);
                MapEditorVerification.Require(point.X == 80 && point.Y == 80, "A hidden point was moved.");
                ObjectOverlay.ShowPoints = true;
                TestGesture(start, moved, true);
                MapEditorVerification.Require(point.X == 80 && point.Y == 80, "Cancelling a point drag changed the model.");
                TestGesture(start, moved, false);
                MapEditorVerification.Require(point.X == 100 && point.Y == 92 && point.Type == 0x7e &&
                    point.Id == 35 && point.MapId == 153 && point.Options == 90, "Point drag changed metadata or projected wrong coordinates.");
                ObjectOverlay.ChangeHistory(true);
                MapEditorVerification.Require(point.X == 80 && point.Y == 80, "Point drag did not undo.");
                ObjectOverlay.ChangeHistory(false);
                MapEditorVerification.Require(point.X == 100 && point.Y == 92, "Point drag did not redo.");

                ObjectOverlay.Tool = MapObjectTool.Range;
                start = new Point(200, 230); moved = new Point(230, 248);
                TestGesture(start, moved, false);
                MapEditorVerification.Require(range.X1 == 90 && range.Y1 == 110, "A hidden range was moved.");
                ObjectOverlay.ShowRanges = true;
                TestGesture(start, moved, false);
                MapEditorVerification.Require(range.X1 == 110 && range.Y1 == 122 && range.X2 == 180 && range.Y2 == 182,
                    "Range translation did not preserve dimensions.");
                TestGesture(new Point(290,293), new Point(308,317), false);
                MapEditorVerification.Require(range.X1 == 110 && range.Y1 == 122 && range.X2 == 192 && range.Y2 == 198 &&
                    range.Id == 9 && range.Destination == 153 && range.Options == 92, "Range resize changed the wrong endpoint or its metadata.");
                ObjectOverlay.ChangeHistory(true);
                MapEditorVerification.Require(range.X2 == 180 && range.Y2 == 182, "Range resize did not undo.");
            }
            finally
            {
                ObjectOverlay = previousOverlay; Finish(); verifying = false;
                View.ZoomAt(0, 0, zoom); View.Pan(ox - View.OriginX, oy - View.OriginY); Invalidate();
            }
        }

        private void VerifyRightClickActions(MapDataInfo fixture)
        {
            ObjectOverlay.ShowPoints = ObjectOverlay.ShowRanges = true;
            var pointer = new Point(200, 230);
            var range = fixture.RangeObjects[0];
            var bounds = ObjectOverlay.Bounds(ViewportMapObjectPositions.Read(range));
            MapEditorVerification.Require(bounds == RectangleF.FromLTRB(155,185,260,275) &&
                bounds.Contains(pointer), "Range preview did not cover its actual projected bounds.");
            Start(this, new MouseEventArgs(MouseButtons.Right, 1, pointer.X, pointer.Y, 0));
            Release(this, new MouseEventArgs(MouseButtons.Right, 1, pointer.X, pointer.Y, 0));
            MapEditorVerification.Require(objectMenu.Items.Count == 2 && objectMenu.Items[0].Text.Contains("點位") &&
                objectMenu.Items[1].Text.Contains("範圍"), "Overlapping point and range did not get independent edit actions.");
            object picked = null;
            Action<object> capture = item => picked = item;
            ObjectEditRequested += capture;
            try { objectMenu.Items[1].PerformClick(); }
            finally { ObjectEditRequested -= capture; }
            MapEditorVerification.Require(picked == range, "Right-click action targeted the wrong map record.");
            var after = new RangeObject { Type = 0x06, Id = 12, Destination = range.Destination,
                Options = range.Options, X1 = 92, Y1 = 105, X2 = 160, Y2 = 170 };
            new RangeObjectService().Update(fixture, 0, after);
            MapEditorVerification.Require(ObjectOverlay.RecordMetadataEdit(range, after) &&
                ObjectOverlay.ChangeHistory(true) && fixture.RangeObjects[0] == range &&
                ObjectOverlay.ChangeHistory(false) && fixture.RangeObjects[0] == after &&
                ObjectOverlay.ChangeHistory(true) && fixture.RangeObjects[0] == range,
                "Right-click field edits did not undo or redo as a unit.");
            ObjectOverlay.ShowPoints = ObjectOverlay.ShowRanges = false;
        }

        private void VerifyHoverDetails()
        {
            MapEditorVerification.Require(ViewportObjectMarkerStyle.ForPoint(0x04).Color !=
                ViewportObjectMarkerStyle.ForPoint(0x03).Color, "NPC and monster spawn colors match.");
            ObjectOverlay.ShowPoints = true;
            ObjectOverlay.ShowRanges = true;
            ObjectOverlay.Tool = MapObjectTool.Collision;
            MovePointer(this, new MouseEventArgs(MouseButtons.None, 0, 73, 170, 0));
            var info = ObjectOverlay.HoverDetails;
            MapEditorVerification.Require(info.Any(line => line.Contains("ID 21") && line.Contains("0x04")) &&
                info.Any(line => line.Contains("X 35") && line.Contains("Y 100")), "NPC hover fields were missing.");
            MovePointer(this, new MouseEventArgs(MouseButtons.None, 0, 365, 238, 0));
            MapEditorVerification.Require(ObjectOverlay.HoverDetails.Any(line => line.Contains("177") && line.Contains("0x03")),
                "Monster spawn tooltip was not independently identified.");
            MovePointer(this, new MouseEventArgs(MouseButtons.None, 0, 200, 230, 0));
            MapEditorVerification.Require(ObjectOverlay.HoverDetails.Any(line => line.Contains("0x01")),
                "The point badge did not take precedence over an overlapping range.");
            ObjectOverlay.Tool = MapObjectTool.Range;
            MovePointer(this, new MouseEventArgs(MouseButtons.None, 0, 200, 230, 0));
            info = ObjectOverlay.HoverDetails;
            MapEditorVerification.Require(info.Any(line => line.Contains("X1 90") && line.Contains("Y1 110")) &&
                info.Any(line => line.Contains("X2 160") && line.Contains("Y2 170")) &&
                info.Any(line => line.Contains("153") && line.Contains("0x0000005C")), "Range hover fields were missing.");
            ObjectOverlay.ShowRanges = false;
            MapEditorVerification.Require(ObjectOverlay.HoverDetails.Length == 0, "A hidden range produced hover text.");
            ObjectOverlay.ShowPoints = false;
            ObjectOverlay.Tool = MapObjectTool.Point;
            ObjectOverlay.ClearHover();
        }

        private void TestGesture(Point start, Point end, bool cancel)
        {
            Start(this, new MouseEventArgs(MouseButtons.Left, 1, start.X, start.Y, 0));
            MovePointer(this, new MouseEventArgs(MouseButtons.Left, 0, end.X, end.Y, 0));
            if (cancel) Finish();
            else Release(this, new MouseEventArgs(MouseButtons.Left, 1, end.X, end.Y, 0));
        }
    }
}
