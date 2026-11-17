using System;
using System.Drawing;
using System.Windows.Forms;

namespace TricksterMap
{
    internal sealed class NewMapDialog : Form
    {
        private readonly NumericUpDown width = Dimension(), height = Dimension();
        public int MapWidth => (int)width.Value;
        public int MapHeight => (int)height.Value;

        public NewMapDialog()
        {
            Text = "新建空白地圖"; ClientSize = new Size(470, 220);
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            var message = new Label { Text = "使用目前地圖的地磚素材，建立新的空白地圖。\r\n不複製原圖的物件、傳點與特效；初始碰撞全部阻擋。",
                AutoSize = false, Bounds = new Rectangle(16, 16, 440, 48) };
            Controls.Add(message);
            Controls.Add(new Label { Text = "寬度（像素）", AutoSize = true, Location = new Point(18, 87) });
            Controls.Add(new Label { Text = "高度（像素）", AutoSize = true, Location = new Point(18, 126) });
            width.Location = new Point(145, 82); height.Location = new Point(145, 121);
            Controls.Add(width); Controls.Add(height);
            var create = new Button { Text = "建立並另存…", Bounds = new Rectangle(232, 175, 125, 30) };
            var cancel = new Button { Text = "取消", Bounds = new Rectangle(365, 175, 85, 30), DialogResult = DialogResult.Cancel };
            create.Click += (s, e) =>
            {
                if (MapWidth % 32 != 0 || MapHeight % 32 != 0)
                { MessageBox.Show(this, "寬度與高度須為32像素的倍數。", Text); return; }
                DialogResult = DialogResult.OK;
            };
            Controls.Add(create); Controls.Add(cancel); AcceptButton = create; CancelButton = cancel;
        }
        private static NumericUpDown Dimension() => new NumericUpDown
        { Minimum = 32, Maximum = 16384, Increment = 32, Value = 1024, Width = 160 };
    }
}
