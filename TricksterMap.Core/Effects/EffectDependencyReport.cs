using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TricksterMap.Scene;

namespace TricksterMap.Effects
{
    public sealed class EffectDependencyReport
    {
        public string EffectPath { get; private set; }
        public EffectDocument Document { get; private set; }
        public EffectResourceResolution Material { get; private set; }
        public NoriAnimationIndex MaterialIndex { get; private set; }
        public List<string> Warnings { get; } = new List<string>();
        public bool HasBlockingIssues { get; private set; }
        public static EffectDependencyReport Inspect(MapDocument map, string resourceRoot = null)
        {
            var report = new EffectDependencyReport { EffectPath = Path.ChangeExtension(map.Md3Path, ".eff") };
            if (!File.Exists(report.EffectPath))
            {
                if (map.Map.EffectObjects.Count > 0) report.Warnings.Add("地圖有特效實例但沒有同名 EFF；可能依賴其他遊戲資源，無法在此預覽。");
                return report;
            }
            try
            {
                report.Document = EffectDocument.Load(report.EffectPath);
                report.Warnings.AddRange(report.Document.Warnings);
                report.Material = EffectResourceResolver.Resolve(report.Document.MaterialReference, report.EffectPath, resourceRoot);
                if (!report.Material.IsResolved) { report.HasBlockingIssues = true; report.Warnings.Add("EFF 材質 " + report.Material.Status + "：" + report.Material.Reference); }
                else if (report.Material.Remapped) report.Warnings.Add("舊作者絕對路徑依 data 後綴解析；原 EFF 未修改。");
                if (report.Material.IsResolved)
                {
                    if (new FileInfo(report.Material.ResolvedPath).Length > 512 * 1024 * 1024) throw new InvalidDataException("EFF 素材超過512 MiB。");
                    using (var stream = File.OpenRead(report.Material.ResolvedPath))
                    {
                        var header = new byte[84]; int read = stream.Read(header, 0, header.Length);
                        bool raw = read == 84 && BitConverter.ToInt32(header, 0) == 0x49524f4e && BitConverter.ToInt32(header, 40) == 0x49574147;
                        bool wrapped = read >= 12 && BitConverter.ToInt32(header, 0) == 0xa0b0 && BitConverter.ToInt32(header, 4) >= 84 &&
                            BitConverter.ToInt32(header, 4) <= 512 * 1024 * 1024 && BitConverter.ToInt32(header, 8) == stream.Length - 12;
                        if ((!raw && !wrapped) || stream.Length > 512 * 1024 * 1024)
                        { report.HasBlockingIssues = true; report.Warnings.Add("EFF 材質不是可支援的 NORI／GAWI 素材：" + report.Material.ResolvedPath); }
                    }
                    if (!report.HasBlockingIssues)
                    {
                        report.MaterialIndex = new NoriAnimationIndex(SceneEnvelope.Unpack(File.ReadAllBytes(report.Material.ResolvedPath), 0xa0b0));
                        report.Warnings.AddRange(report.MaterialIndex.Warnings);
                        foreach (var cream in report.Document.Creams.Values)
                        {
#if NET10_0_OR_GREATER
                            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
#endif
                            byte[] name = Encoding.GetEncoding(949, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback).GetBytes(cream.Text("TextureName"));
                            if (name.Length > 32 || !report.MaterialIndex.Animations.ContainsKey(Convert.ToBase64String(name)))
                            { report.HasBlockingIssues = true; report.Warnings.Add("EFF 材質沒有唯一動畫：" + cream.Text("TextureName")); }
                        }
                    }
                }
                foreach (byte[] raw in map.Map.EffectObjects)
                {
                    string name = new MapEffectInstance(raw).Name;
                    if (!report.Document.Cakes.ContainsKey(name)) { report.HasBlockingIssues = true; report.Warnings.Add("特效實例找不到唯一 CAKE：" + name); }
                }
                if (report.Document.Warnings.Count > 0) report.HasBlockingIssues = true;
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is System.Xml.XmlException || ex is ArgumentException || ex is OverflowException || ex is DecoderFallbackException)
            { report.HasBlockingIssues = true; report.Warnings.Add("EFF 無法解析：" + ex.Message); }
            return report;
        }
    }
}
