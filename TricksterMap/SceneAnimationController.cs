using System;
using System.Diagnostics;
using System.Windows.Forms;
using TricksterMap.SceneView;

namespace TricksterMap
{
    /// <summary>Preview time changes rendering only, never the scene's serialized model.</summary>
    internal sealed class SceneAnimationController : IDisposable
    {
        private readonly Timer timer = new Timer { Interval = 100 };
        private readonly Stopwatch clock = new Stopwatch();
        private SceneRenderer renderer;
        private readonly Action invalidate;
        private readonly ToolStripMenuItem play = new ToolStripMenuItem("播放場景動畫")
        { CheckOnClick = true, Enabled = false };
        public ToolStripMenuItem Menu { get; } = new ToolStripMenuItem("預覽");
        private readonly ToolStripMenuItem effects = new ToolStripMenuItem("顯示地圖 EFF 特效") { CheckOnClick = true, Checked = true };
        public event Action ResourceRootRequested;

        public SceneAnimationController(Action invalidate)
        {
            this.invalidate = invalidate; Menu.DropDownItems.Add(play);
            Menu.DropDownItems.Add(effects);
            var resources = new ToolStripMenuItem("特效素材依賴／素材根目錄…");
            resources.Click += (s, e) => ResourceRootRequested?.Invoke(); Menu.DropDownItems.Add(resources);
            effects.CheckedChanged += (s, e) => { if (renderer != null) renderer.ShowEffects = effects.Checked; invalidate(); };
            play.CheckedChanged += (s, e) => Update();
            timer.Tick += (s, e) =>
            {
                if (renderer == null) return;
                renderer.AnimationTimeSeconds = clock.Elapsed.TotalSeconds;
                invalidate();
            };
        }

        public void Bind(SceneRenderer value)
        {
            timer.Stop(); renderer = value;
            if (renderer != null) renderer.ShowEffects = effects.Checked;
            play.Enabled = value != null && value.HasAnimatedObjects;
            if (!play.Enabled) play.Checked = false;
            clock.Restart(); Update();
        }

        private void Update()
        {
            if (renderer != null) renderer.AnimateObjects = play.Checked;
            if (renderer != null && play.Checked) timer.Start(); else timer.Stop();
            invalidate();
        }

        public void Dispose() { timer.Stop(); timer.Dispose(); }
    }
}
