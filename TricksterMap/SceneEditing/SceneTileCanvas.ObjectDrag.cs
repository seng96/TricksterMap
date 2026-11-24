using System;
using System.Drawing;
using System.Windows.Forms;
using TricksterMap.Scene;
using TricksterMap.SceneView;

namespace TricksterMap.SceneEditing
{
    internal sealed partial class SceneTileCanvas
    {
        private SceneObject draggedObject;
        private SceneLayer draggedLayer;
        private Point objectStart;
        private SceneObjectMovePreview objectMove;
        internal SceneLayer ObjectLayer { get; set; }
        internal SceneObject SelectedObject { get; set; }
        internal event Action<SceneObject> ObjectSelected;
        internal event Action<SceneObject, int, int> ObjectMoved;

        private PointF? DragMarker => objectMove == null ? (PointF?)null :
            new PointF((float)((double)draggedLayer.X + objectMove.X),
                (float)((double)draggedLayer.Y + objectMove.Y - draggedObject.Z));

        private SceneObject FindObject(Point point)
        {
            if (ObjectLayer == null) return null;
            if (SelectedObject != null && SelectionMarkerWorld.HasValue)
            {
                var marker = SelectionMarkerWorld.Value;
                if (Math.Abs(point.X - View.ScreenX(marker.X)) <= 7 &&
                    Math.Abs(point.Y - View.ScreenY(marker.Y)) <= 7) return SelectedObject;
            }
            return renderer.PickObject(ObjectLayer, View.WorldX(point.X), View.WorldY(point.Y));
        }

        private void StartObjectDrag(Point point)
        {
            var item = FindObject(point);
            if (item == null) return;
            ObjectSelected?.Invoke(item);
            draggedObject = item;
            draggedLayer = ObjectLayer;
            objectStart = point;
            if (!verifying) Capture = true;
        }

        private void UpdateObjectDrag(Point point)
        {
            var threshold = SystemInformation.DragSize;
            if (objectMove == null && Math.Abs((double)point.X - objectStart.X) < threshold.Width / 2.0 &&
                Math.Abs((double)point.Y - objectStart.Y) < threshold.Height / 2.0) return;
            int x = Offset(draggedObject.X, (point.X - (double)objectStart.X) / View.Zoom);
            int y = Offset(draggedObject.Y, (point.Y - (double)objectStart.Y) / View.Zoom);
            objectMove = new SceneObjectMovePreview(draggedObject, x, y);
            Cursor = Cursors.SizeAll;
            PositionChanged?.Invoke($"拖曳中  X {x}  Y {y}  ·  放開左鍵套用  ·  Esc 取消");
            Invalidate();
        }

        private static int Offset(int start, double delta)
            => (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, start + Math.Round(delta, MidpointRounding.AwayFromZero)));

        private void CommitObjectDrag(Point point)
        {
            if (draggedObject == null) return;
            UpdateObjectDrag(point);
            var move = objectMove;
            CancelObjectDrag();
            if (move != null && (move.X != move.Item.X || move.Y != move.Item.Y))
            {
                ObjectMoved?.Invoke(move.Item, move.X, move.Y);
                PositionChanged?.Invoke($"物件已移至 X {move.X}  Y {move.Y}  ·  Ctrl+Z 復原");
            }
        }

        private void CancelObjectDrag()
        {
            if (draggedObject == null) return;
            draggedObject = null;
            draggedLayer = null;
            objectMove = null;
            Invalidate();
        }
    }
}
