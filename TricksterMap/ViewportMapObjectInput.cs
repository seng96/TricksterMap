using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace TricksterMap
{
    internal sealed partial class CollisionCanvas
    {
        internal ViewportMapObjectOverlay ObjectOverlay { get; set; }
        internal bool EditingMapObjects => ObjectOverlay != null && ObjectOverlay.Tool != MapObjectTool.Collision;
        internal event EventHandler ObjectPositionsChanged;
        internal event Action<object> ObjectEditRequested;
        private ContextMenuStrip objectMenu;
        private bool rightClickPending;

        internal void ShowMapObjectStatus()
        {
            if (EditingMapObjects) PositionChanged?.Invoke(ObjectOverlay.SelectionText);
        }

        internal void ChangeObjectHistory(bool reverse)
        {
            Finish();
            if (ObjectOverlay?.ChangeHistory(reverse) == true)
            {
                ShowMapObjectStatus();
                ObjectPositionsChanged?.Invoke(this, EventArgs.Empty);
                Invalidate();
            }
        }

        private void Release(object sender, MouseEventArgs e)
        {
            bool changed = e.Button == MouseButtons.Left && ObjectOverlay?.Commit(e.Location) == true;
            bool menu = e.Button == MouseButtons.Right && rightClickPending && WithinRightClickThreshold(e.Location);
            Finish();
            if (menu) ShowMapObjectMenu(e.Location);
            if (changed)
            {
                ObjectPositionsChanged?.Invoke(this, EventArgs.Empty);
                ShowMapObjectStatus();
            }
        }

        private bool WithinRightClickThreshold(Point point)
            => Math.Abs(point.X - (double)panStart.X) < Math.Max(3, SystemInformation.DragSize.Width / 2.0) &&
                Math.Abs(point.Y - (double)panStart.Y) < Math.Max(3, SystemInformation.DragSize.Height / 2.0);

        private void ShowMapObjectMenu(Point pointer)
        {
            if (objectMenu == null) objectMenu = new ContextMenuStrip();
            foreach (var previous in objectMenu.Items.Cast<ToolStripItem>().ToArray()) previous.Dispose();
            objectMenu.Items.Clear();
            AddObjectMenuGroup("點位", ObjectOverlay.PointsAt(pointer).Reverse().Cast<object>().ToArray());
            AddObjectMenuGroup("範圍", ObjectOverlay.RangesAt(pointer).Reverse().Cast<object>().ToArray());
            if (!verifying && objectMenu.Items.Count > 0) objectMenu.Show(this, pointer);
        }

        private void AddObjectMenuGroup(string name, object[] targets)
        {
            if (targets.Length == 0) return;
            if (targets.Length == 1)
            {
                objectMenu.Items.Add(EditMenuItem(targets[0], "編輯 " + ObjectOverlay.MenuCaption(targets[0]) + "…"));
                return;
            }
            var group = new ToolStripMenuItem($"編輯{name}（{targets.Length}）");
            foreach (var item in targets)
                group.DropDownItems.Add(EditMenuItem(item, ObjectOverlay.MenuCaption(item) + "…"));
            objectMenu.Items.Add(group);
        }

        private ToolStripMenuItem EditMenuItem(object target, string title)
        {
            var item = new ToolStripMenuItem(title);
            item.Click += (s, e) => { objectMenu.Close(); ObjectEditRequested?.Invoke(target); };
            return item;
        }
    }
}
