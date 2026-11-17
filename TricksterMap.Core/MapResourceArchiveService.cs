using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Xml;
using TricksterMap.Effects;
using TricksterMap.Scene;

namespace TricksterMap
{
    /// <summary>Fresh ZIP with game-relative layout, declared EFF material and integrity manifest.</summary>
    public sealed class MapResourceArchiveService
    {
        public void Export(MapDocument source, string destination, SceneDocument scene = null, string resourceRoot = null)
        {
            string target = Path.GetFullPath(destination), parent = Path.GetDirectoryName(target);
            if (Path.GetExtension(target).ToLowerInvariant() != ".zip") throw new ArgumentException("資源包需使用 .zip 副檔名。");
            if (File.Exists(target) || Directory.Exists(target)) throw new IOException("不覆寫既有資源包：" + target);
            if (!Directory.Exists(parent)) throw new DirectoryNotFoundException(parent);
            var dependencies = EffectDependencyReport.Inspect(source, resourceRoot);
            if (dependencies.HasBlockingIssues) throw new InvalidDataException("素材依賴檢查未通過：" + string.Join("；", dependencies.Warnings));
            string stage = Path.Combine(parent, ".TricksterMap-resources-" + Guid.NewGuid().ToString("N"));
            string archive = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            Directory.CreateDirectory(stage);
            try
            {
                string maps = Path.Combine(stage, "data", "map"); Directory.CreateDirectory(maps);
                string mapPath = Path.Combine(maps, Path.GetFileNameWithoutExtension(target) + ".md3");
                new MapBundleExportService().Export(source, mapPath, scene);
                if (dependencies.Document != null)
                {
                    var material = dependencies.Material;
                    string game = material.GamePath;
                    if (!game.StartsWith("data/", StringComparison.OrdinalIgnoreCase)) game = "data/map/" + game;
                    string output = Path.GetFullPath(Path.Combine(stage, game.Replace('/', Path.DirectorySeparatorChar)));
                    if (!output.StartsWith(stage + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("材質路徑超出資源包。");
                    if (File.Exists(output))
                    { if (Hash(output) != Hash(material.ResolvedPath)) throw new IOException("素材與輸出地圖檔案名稱衝突：" + game); }
                    else { Directory.CreateDirectory(Path.GetDirectoryName(output)); File.Copy(material.ResolvedPath, output); }
                    // Attribute order and unknown nodes remain in their original order.
                    File.WriteAllBytes(Path.ChangeExtension(mapPath, ".eff"), dependencies.Document.WithMaterialReference(game.Replace('/', '\\')));
                }
                using (var writer = XmlWriter.Create(Path.Combine(stage, "asset-manifest.xml"), new XmlWriterSettings { Indent = true }))
                {
                    writer.WriteStartElement("MapResources"); writer.WriteAttributeString("map", "data/map/" + Path.GetFileName(mapPath));
                    foreach (string file in Directory.GetFiles(stage, "*", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
                    {
                        if (Path.GetFileName(file) == "asset-manifest.xml") continue;
                        writer.WriteStartElement("Asset"); writer.WriteAttributeString("path", file.Substring(stage.Length + 1).Replace('\\', '/'));
                        writer.WriteAttributeString("sha256", Hash(file)); writer.WriteAttributeString("bytes", new FileInfo(file).Length.ToString(System.Globalization.CultureInfo.InvariantCulture)); writer.WriteEndElement();
                    }
                    foreach (string warning in dependencies.Warnings) writer.WriteElementString("Warning", warning);
                    writer.WriteEndElement();
                }
                using (var output = new FileStream(archive, FileMode.CreateNew))
                using (var zip = new ZipArchive(output, ZipArchiveMode.Create))
                    foreach (string file in Directory.GetFiles(stage, "*", SearchOption.AllDirectories))
                    { var entry = zip.CreateEntry(file.Substring(stage.Length + 1).Replace('\\', '/'), CompressionLevel.Optimal); using (var stream = entry.Open()) using (var input = File.OpenRead(file)) input.CopyTo(stream); }
                File.Move(archive, target);
            }
            finally { if (File.Exists(archive)) File.Delete(archive); Directory.Delete(stage, true); }
        }
        private static string Hash(string path)
        { using (var sha = SHA256.Create()) using (var file = File.OpenRead(path)) return BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "").ToLowerInvariant(); }
    }
}
