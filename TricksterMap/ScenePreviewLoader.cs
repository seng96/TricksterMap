using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TricksterMap.SceneView;

namespace TricksterMap
{
    /// <summary>Coalesces map loads; stale workers never replace the current preview.</summary>
    internal sealed class ScenePreviewLoader : IDisposable
    {
        private static readonly SemaphoreSlim worker = new SemaphoreSlim(1, 1);
        private readonly Control owner;
        private readonly object pendingLock = new object();
        private readonly HashSet<SceneRenderer> pending = new HashSet<SceneRenderer>();
        private CancellationTokenSource cancellation;
        private int epoch;
        private bool disposed;
        public SceneRenderer Current { get; private set; }
        public string InitialFingerprint { get; private set; }
        public event Action<SceneRenderer> Ready;

        public ScenePreviewLoader(Control owner) { this.owner = owner; }

        public void Load(MapDocument document, IList<MapCollisionEditorForm> editors)
        {
            int request = ++epoch;
            cancellation?.Cancel(); cancellation?.Dispose();
            cancellation = new CancellationTokenSource();
            CancellationToken token = cancellation.Token;
            lock (pendingLock) { foreach (var stale in pending) stale.Dispose(); pending.Clear(); }
            foreach (var editor in editors)
            {
                editor.SetSceneRenderer(null);
                editor.SetSceneStatus("正在載入地圖樣式… 可先使用簡易模式編輯碰撞。");
            }
            Current?.Dispose(); Current = null;
            // Creating a handle does not show or activate the form. It also makes
            // BeginInvoke safe when the map was supplied on the command line.
            var handle = owner.Handle;
            Task.Run(async () =>
            {
                SceneRenderer result = null;
                string initialFingerprint = null;
                Exception failure = null;
                bool acquired = false;
                try
                {
                    await worker.WaitAsync(token).ConfigureAwait(false); acquired = true;
                    token.ThrowIfCancellationRequested();
                    result = SceneRenderer.Load(document);
                    initialFingerprint = MapEditFingerprint.ComputeScene(result.Scene);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex) { failure = ex; }
                finally { if (acquired) worker.Release(); }
                if (disposed || token.IsCancellationRequested || owner.IsDisposed)
                { result?.Dispose(); return; }
                SceneRenderer completed = result;
                lock (pendingLock)
                {
                    if (disposed || token.IsCancellationRequested) { completed?.Dispose(); return; }
                    if (completed != null) pending.Add(completed);
                }
                try
                {
                    owner.BeginInvoke((Action)(() =>
                    {
                        lock (pendingLock) { if (completed != null) pending.Remove(completed); }
                        if (disposed || request != epoch || owner.IsDisposed)
                        { completed?.Dispose(); return; }
                        Current = completed;
                        InitialFingerprint = initialFingerprint;
                        foreach (var editor in editors)
                            if (!editor.IsDisposed)
                            {
                                editor.SetSceneRenderer(completed);
                                editor.SetSceneStatus(failure == null ? "地圖樣式已載入；可切換精細／簡易預覽。" :
                                    "地圖樣式無法載入：" + failure.Message + "；簡易模式仍可編輯。");
                            }
                        if (completed != null)
                            try { Ready?.Invoke(completed); }
                            catch (Exception ex)
                            {
                                foreach (var editor in editors)
                                    if (!editor.IsDisposed) editor.SetSceneStatus("場景製作介面無法載入：" + ex.Message);
                            }
                    }));
                }
                catch (InvalidOperationException)
                { lock (pendingLock) { if (completed != null) pending.Remove(completed); } completed?.Dispose(); }
            });
        }

        public void Dispose()
        {
            disposed = true; epoch++;
            cancellation?.Cancel(); cancellation?.Dispose();
            lock (pendingLock) { foreach (var waiting in pending) waiting.Dispose(); pending.Clear(); }
            Current?.Dispose(); Current = null;
        }
    }
}
