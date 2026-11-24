using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using TricksterMap.Scene;

namespace TricksterMap.SceneEditing
{
    internal sealed partial class SceneTileCanvas
    {
        internal void VerifyDragGesture(Point from, Point to, bool cancel, Action verifyPending)
        {
            verifying = true;
            try
            {
                Start(this, new MouseEventArgs(MouseButtons.Left, 1, from.X, from.Y, 0));
                MovePointer(this, new MouseEventArgs(MouseButtons.Left, 0, to.X, to.Y, 0));
                verifyPending();
                if (cancel) FinishInteraction();
                else Release(this, new MouseEventArgs(MouseButtons.Left, 1, to.X, to.Y, 0));
            }
            finally { verifying = false; FinishInteraction(); }
        }
        internal bool HasDragPreview => objectMove != null;
    }

    public sealed partial class SceneObjectEditorForm
    {
        internal void VerifyObjectDrag()
        {
            int index = objects.SelectedIndex;
            var item = layer.Objects[index];
            // Nonzero Z and a fractional zoom verify the drag preserves the anchor and translates world X/Y only.
            current.SetPosition(index, item.X, item.Y, 17);
            SelectObject();
            canvas.View.ZoomAt(canvas.Width / 2, canvas.Height / 2, 1.5);
            CenterObject();
            var spot = renderer.VerificationGrabPoint(layer, item);
            canvas.View.Pan(canvas.Width / 2.0 - canvas.View.ScreenX(spot.X),
                canvas.Height / 2.0 - canvas.View.ScreenY(spot.Y));
            var from = new Point((int)Math.Round(canvas.View.ScreenX(spot.X)), (int)Math.Round(canvas.View.ScreenY(spot.Y)));
            var to = new Point(from.X + 30, from.Y - 18);
            byte[] before = (byte[])item.RawData.Clone();
            Action pending = () => MapEditorVerification.Require(canvas.HasDragPreview && before.SequenceEqual(item.RawData),
                "Object drag preview modified the source record before release.");
            canvas.VerifyDragGesture(from, to, true, pending);
            MapEditorVerification.Require(before.SequenceEqual(item.RawData) && !canvas.HasDragPreview,
                "Cancelling a drag changed the record.");
            int expectedX = item.X + 20, expectedY = item.Y - 12;
            canvas.VerifyDragGesture(from, to, false, pending);
            MapEditorVerification.Require(item.X == expectedX && item.Y == expectedY && item.Z == 17 &&
                item.RawData.Take(4).SequenceEqual(before.Take(4)) && item.RawData.Skip(12).SequenceEqual(before.Skip(12)),
                "Drag changed fields other than X/Y or moved by the wrong world delta.");
            ChangeHistory(true);
            MapEditorVerification.Require(before.SequenceEqual(item.RawData), "One undo did not restore the drag.");
            ChangeHistory(false);
            MapEditorVerification.Require(item.X == expectedX && item.Y == expectedY, "Redo did not restore the drag.");
        }
    }
}

namespace TricksterMap.SceneView
{
    public sealed partial class SceneRenderer
    {
        internal PointF VerificationGrabPoint(SceneLayer layer, SceneObject item)
        {
            foreach (var sprite in CurrentSprites(item))
            {
                if (sprite.Opacity == 0) continue;
                for (int sy = 0; sy < sprite.Height; sy++)
                    for (int sx = 0; sx < sprite.Width; sx++)
                    {
                        if (sprite.Image.GetPixel(sx, sy).A == 0) continue;
                        int x = sprite.X + ((sprite.Flip & 1) == 0 ? sx : sprite.Width - 1 - sx);
                        int y = sprite.Y + ((sprite.Flip & 2) == 0 ? sy : sprite.Height - 1 - sy);
                        if (Math.Abs(x) < 12 && Math.Abs(y) < 12) continue;
                        var point = new PointF(layer.X + item.X + x + .5f, layer.Y + item.Y - item.Z + y + .5f);
                        if (PickObject(layer, point.X, point.Y) == item) return point;
                    }
            }
            return new PointF(layer.X + item.X, layer.Y + item.Y - item.Z);
        }
    }
}
