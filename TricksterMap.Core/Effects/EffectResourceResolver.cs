using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TricksterMap.Effects
{
    public sealed class EffectResourceResolution
    {
        public string Reference { get; internal set; }
        public string GamePath { get; internal set; }
        public string ResolvedPath { get; internal set; }
        public List<string> Candidates { get; } = new List<string>();
        public string Status { get; internal set; }
        public bool Remapped { get; internal set; }
        public bool IsResolved => Status == "resolved";
    }
    public static class EffectResourceResolver
    {
        public static EffectResourceResolution Resolve(string reference, string effectPath, string resourceRoot = null)
        {
            var result = new EffectResourceResolution { Reference = reference ?? "", Status = "missing" };
            string normalized = (reference ?? "").Trim().Replace('\\', '/');
            var parts = normalized.Split('/');
            if (normalized.Length == 0 || parts.Any(part => part == ".." || part == ".") || normalized.IndexOfAny(new[] { '\0', '*', '?', '"', '<', '>', '|' }) >= 0)
            { result.Status = "invalid"; return result; }
            int data = Array.FindIndex(parts, part => string.Equals(part, "data", StringComparison.OrdinalIgnoreCase));
            bool rooted = normalized.StartsWith("/") || normalized.Length > 1 && normalized[1] == ':';
            string game = data >= 0 ? string.Join("/", parts.Skip(data)) : !rooted ? normalized : null;
            if (game == null || game.Contains(":")) { result.Status = "invalid"; return result; }
            result.GamePath = game; result.Remapped = rooted && data >= 0;
            var candidates = new List<string>();
            string effectDirectory = effectPath == null ? null : Path.GetDirectoryName(Path.GetFullPath(effectPath));
            Action<string, string> add = (root, relative) =>
            {
                if (root == null) return;
                string path = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
                string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && File.Exists(path) && !candidates.Contains(path, StringComparer.OrdinalIgnoreCase)) candidates.Add(path);
            };
            if (resourceRoot != null)
            {
                add(resourceRoot, game);
                if (string.Equals(new DirectoryInfo(resourceRoot).Name, "data", StringComparison.OrdinalIgnoreCase) && game.StartsWith("data/", StringComparison.OrdinalIgnoreCase)) add(resourceRoot, game.Substring(5));
                if (candidates.Count == 1) { result.Candidates.Add(candidates[0]); result.Status = "resolved"; result.ResolvedPath = candidates[0]; return result; }
            }
            // Full game-relative paths get an exact root. Flat map corpora may
            // supply the declared data/map suffix beside the EFF, never an arbitrary basename.
            if (game.StartsWith("data/", StringComparison.OrdinalIgnoreCase))
            {
                for (var directory = effectDirectory == null ? null : new DirectoryInfo(effectDirectory); directory != null; directory = directory.Parent)
                {
                    if (string.Equals(directory.Name, "data", StringComparison.OrdinalIgnoreCase)) { add(directory.Parent?.FullName, game); break; }
                }
                if (game.StartsWith("data/map/", StringComparison.OrdinalIgnoreCase)) add(effectDirectory, game.Substring(9));
            }
            else add(effectDirectory, game);
            result.Candidates.AddRange(candidates);
            if (candidates.Count == 1) { result.Status = "resolved"; result.ResolvedPath = candidates[0]; }
            else if (candidates.Count > 1) result.Status = "ambiguous";
            return result;
        }
    }
}
