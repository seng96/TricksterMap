using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using TricksterMap.SceneView;

namespace TricksterMap
{
    public partial class MDIParent
    {
        private void ShowEffectResources()
        {
            var renderer = sceneLoader.Current;
            if (renderer == null) { MessageBox.Show(this, "請先載入地圖場景。", "特效素材"); return; }
            using (var form = new EffectResourcesForm(renderer, () => { animation.Bind(renderer); Refresh(); foreach (var editor in collisionEditors) editor.Canvas.Invalidate(); }))
                form.ShowDialog(this);
        }
        private void ExportResourceArchive()
        {
            if (document == null || sceneLoader.Current == null) { MessageBox.Show(this, "請先載入地圖場景。", "匯出資源包"); return; }
            using (var output = new SaveFileDialog { Filter = "完整遊戲資源包 ZIP|*.zip", FileName = Path.GetFileNameWithoutExtension(document.Md3Path) + "_edited.zip" })
            {
                if (output.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    sceneEditor?.FinishInteraction(); objectEditor?.FinishInteraction();
                    new MapResourceArchiveService().Export(document, output.FileName, sceneLoader.Current.Scene, sceneLoader.Current.ResourceRoot);
                    MessageBox.Show(this, "資源包已匯出，包含地圖四檔、已有的 EFF 與其指定素材，以及檔案核對清單。", "匯出完成");
                }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "匯出資源包失敗", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }
    }
    internal sealed class EffectResourcesForm : Form
    {
        private readonly SceneRenderer renderer;
        private readonly TextBox text = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill, WordWrap = false };
        internal EffectResourcesForm(SceneRenderer renderer, Action refreshed)
        {
            this.renderer = renderer; Text = "地圖特效與素材依賴"; Size = new Size(900, 570); StartPosition = FormStartPosition.CenterParent;
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40 };
            var choose = new Button { Text = "選擇遊戲素材根目錄…", AutoSize = true };
            var reset = new Button { Text = "使用地圖所在目錄", AutoSize = true };
            buttons.Controls.Add(choose); buttons.Controls.Add(reset); Controls.Add(text); Controls.Add(buttons);
            choose.Click += (s, e) =>
            {
                using (var folder = new FolderBrowserDialog { Description = "選擇包含 data 資料夾的遊戲根目錄，或 data 本身。" })
                    if (folder.ShowDialog(this) == DialogResult.OK) LoadRoot(folder.SelectedPath, refreshed);
            };
            reset.Click += (s, e) => LoadRoot(null, refreshed); RefreshReport();
        }
        private void LoadRoot(string root, Action refreshed)
        {
            try { UseWaitCursor = true; renderer.LoadEffectResources(root); RefreshReport(); refreshed(); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "素材載入失敗"); }
            finally { UseWaitCursor = false; }
        }
        private void RefreshReport()
        {
            var dependencies = renderer.EffectDependencies; var material = dependencies?.Material; var document = dependencies?.Document;
            text.Text = "EFF：" + dependencies?.EffectPath + Environment.NewLine +
                "素材根目錄：" + (renderer.ResourceRoot ?? "由地圖位置解析") + Environment.NewLine +
                "原始素材引用：" + (document?.MaterialReference ?? "無") + Environment.NewLine +
                "狀態：" + (material?.Status ?? "沒有同名 EFF") + Environment.NewLine +
                "素材依賴檢查：" + (dependencies != null && dependencies.HasBlockingIssues ? "未通過，請查看下方原因" : "通過") + Environment.NewLine +
                "實際素材：" + (material?.ResolvedPath ?? "未解析") + Environment.NewLine +
                "CAKE：" + (document?.Cakes.Count ?? 0) + "；CREAM：" + (document?.Creams.Count ?? 0) + Environment.NewLine + Environment.NewLine +
                string.Join(Environment.NewLine, renderer.Warnings) + Environment.NewLine +
                (material == null ? "" : string.Join(Environment.NewLine, material.Candidates.Select(path => "候選：" + path)));
        }
    }
}
