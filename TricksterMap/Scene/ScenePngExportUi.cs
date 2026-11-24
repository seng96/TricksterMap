using System;
using System.IO;
using System.Windows.Forms;
using TricksterMap.SceneView;

namespace TricksterMap
{
    public partial class MDIParent
    {
        private readonly ToolStripMenuItem exportScenePng = new ToolStripMenuItem("匯出全圖 PNG…") { Enabled = false };

        private void AttachScenePngExport()
        {
            fileMenu.DropDownItems.Add(exportScenePng);
            exportScenePng.Click += (s, e) =>
            {
                var renderer = sceneLoader.Current;
                if (renderer == null || renderer.Scene.Document != document) return;
                using (var dialog = new SaveFileDialog
                {
                    Filter = "PNG 圖片 (*.png)|*.png", DefaultExt = "png", AddExtension = true,
                    FileName = Path.GetFileNameWithoutExtension(document.Md3Path) + ".png",
                })
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    try
                    {
                        UseWaitCursor = true;
                        ExportCurrentScenePng(dialog.FileName);
                    }
                    catch (Exception ex)
                    { MessageBox.Show(this, ex.Message, "PNG 匯出失敗", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                    finally { UseWaitCursor = false; }
                }
            };
        }

        internal void ExportCurrentScenePng(string fileName)
        {
            var renderer = sceneLoader.Current;
            if (renderer == null || renderer.Scene.Document != document)
                throw new InvalidOperationException("地圖場景尚未載入。");
            sceneEditor?.FinishInteraction();
            objectEditor?.FinishInteraction();
            ScenePngExporter.Export(renderer, fileName);
        }
    }
}
