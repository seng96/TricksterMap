using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace TricksterMap.Effects
{
    public sealed class EffectCream
    {
        private readonly List<KeyValuePair<string, string>> fields;
        internal EffectCream(XElement node)
        { Name = (string)node.Attribute("Name") ?? ""; fields = node.Elements().Select(e => new KeyValuePair<string, string>(e.Name.LocalName, e.Value)).ToList(); }
        public string Name { get; }
        public IEnumerable<KeyValuePair<string, string>> Fields => fields;
        public string Text(string key, string fallback = "") => fields.LastOrDefault(p => p.Key == key).Value ?? fallback;
        public double Number(string key, double fallback = 0) => Parse(Text(key), fallback);
        public static double Parse(string value, double fallback = 0)
        { double result; return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result) && !double.IsNaN(result) && !double.IsInfinity(result) ? result : fallback; }
        public EffectVector Vector(string key, EffectVector fallback = default(EffectVector))
        {
            string text = Text(key); if (text.Length == 0) return fallback;
            var parts = text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            return new EffectVector(parts.Length > 0 ? Parse(parts[0]) : 0, parts.Length > 1 ? Parse(parts[1]) : 0, parts.Length > 2 ? Parse(parts[2]) : 0);
        }
        public double[] OrderedXY(string key, double fallback)
        {
            var result = new[] { fallback, fallback };
            foreach (var field in fields)
                if (field.Key == key) result[0] = result[1] = (float)Parse(field.Value);
                else if (field.Key == key + "X") result[0] = (float)Parse(field.Value);
                else if (field.Key == key + "Y") result[1] = (float)Parse(field.Value);
            return result;
        }
    }
    public struct EffectVector
    {
        public EffectVector(double x, double y, double z) { X = (float)x; Y = (float)y; Z = (float)z; }
        public double X, Y, Z;
        public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);
        public EffectVector Normalized => Length > 0 ? this / Length : new EffectVector();
        public static EffectVector operator +(EffectVector a, EffectVector b) => new EffectVector(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static EffectVector operator -(EffectVector a, EffectVector b) => new EffectVector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static EffectVector operator *(EffectVector a, double b) => new EffectVector(a.X * b, a.Y * b, a.Z * b);
        public static EffectVector operator /(EffectVector a, double b) => new EffectVector(a.X / b, a.Y / b, a.Z / b);
    }
    public sealed class EffectChild
    { public string Name { get; internal set; } public string Sort { get; internal set; } public double Time { get; internal set; } }
    public sealed class EffectPlay
    { public string Mode { get; internal set; } public double Time { get; internal set; } }
    public sealed class EffectCake
    {
        public string Name { get; internal set; }
        public List<EffectChild> Children { get; } = new List<EffectChild>();
        public List<EffectPlay> Plays { get; } = new List<EffectPlay>();
    }
    public sealed class EffectDocument
    {
        public string Path { get; private set; }
        public byte[] Original { get; private set; }
        public string MaterialReference { get; private set; }
        public Dictionary<string, EffectCake> Cakes { get; } = new Dictionary<string, EffectCake>(StringComparer.Ordinal);
        public Dictionary<string, EffectCream> Creams { get; } = new Dictionary<string, EffectCream>(StringComparer.Ordinal);
        public List<string> Warnings { get; } = new List<string>();
        public static EffectDocument Load(string path)
        { if (new FileInfo(path).Length > 16 * 1024 * 1024) throw new InvalidDataException("EFF XML exceeds 16 MiB."); return Parse(File.ReadAllBytes(path), System.IO.Path.GetFullPath(path)); }
        public static EffectDocument Parse(byte[] raw, string path = null)
        {
            if (raw == null || raw.Length > 16 * 1024 * 1024) throw new InvalidDataException("EFF XML exceeds 16 MiB.");
            XDocument xml;
            using (var input = new MemoryStream(raw))
            using (var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 16 * 1024 * 1024 }))
                xml = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
            var root = xml.Root;
            if (root == null || root.Name != "CAKEWAPPER") throw new InvalidDataException("EFF requires a CAKEWAPPER root.");
            var result = new EffectDocument { Path = path, Original = (byte[])raw.Clone(), MaterialReference = (string)root.Attribute("NoriFileName") ?? "" };
            var duplicateCakes = new HashSet<string>(); var duplicateCreams = new HashSet<string>();
            foreach (var node in root.Elements("CREAM"))
            {
                var cream = new EffectCream(node);
                if (result.Creams.ContainsKey(cream.Name)) duplicateCreams.Add(cream.Name); else result.Creams.Add(cream.Name, cream);
            }
            foreach (string name in duplicateCreams) { result.Creams.Remove(name); result.Warnings.Add("重複 CREAM：" + name); }
            foreach (var node in root.Elements("CAKE"))
            {
                var cake = new EffectCake { Name = (string)node.Attribute("Name") ?? "" };
                var registered = new HashSet<string>(StringComparer.Ordinal);
                var children = new List<Tuple<XElement, string, double>>();
                foreach (var child in node.Elements("SUBCREAM"))
                {
                    // Native registration key (@Name) is separate from the CREAM reference.
                    string name = (string)child.Attribute("Name") ?? "", key = name; int suffix = 1;
                    while (!registered.Add(key)) key = name + "(" + (++suffix) + ")";
                    double time = EffectCream.Parse((string)child.Attribute("Time"));
                    int before = time > 0 ? children.FindIndex(item => time < item.Item3) : -1;
                    var entry = Tuple.Create(child, key, time);
                    if (before < 0) children.Add(entry); else children.Insert(before, entry);
                }
                foreach (var play in node.Elements("CAKEPLAY")) cake.Plays.Add(new EffectPlay { Time = EffectCream.Parse((string)play.Attribute("Time")), Mode = (string)play.Attribute("Type") ?? "" });
                var sentinels = new Dictionary<string, string> { { "_LOOP_", "PlayLoop" }, { "_RESTART_", "PlayRestart" }, { "_STOP_", "PlayStop" }, { "end", "PlayNormal" } };
                foreach (var entry in children.ToArray())
                {
                    string mode;
                    if (sentinels.TryGetValue(entry.Item2, out mode))
                    {
                        // 0x985e00: Loop/Normal use the current child-list tail time;
                        // Restart/Stop retain their own registered start time.
                        double time = mode == "PlayLoop" || mode == "PlayNormal" ? children.Last().Item3 : entry.Item3;
                        cake.Plays.Add(new EffectPlay { Time = time, Mode = mode }); children.Remove(entry);
                    }
                    else
                    {
                        string name = (string)entry.Item1.Element("SubCreamName") ?? "";
                        cake.Children.Add(new EffectChild { Time = entry.Item3, Name = name, Sort = (string)entry.Item1.Element("SubCreamSortType") ?? "Map_Over" });
                        if (!result.Creams.ContainsKey(name)) result.Warnings.Add("缺少 CREAM：" + name);
                    }
                }
                var ordered = cake.Plays.OrderBy(play => play.Time).ToArray(); cake.Plays.Clear(); cake.Plays.AddRange(ordered);
                if (result.Cakes.ContainsKey(cake.Name)) duplicateCakes.Add(cake.Name); else result.Cakes.Add(cake.Name, cake);
            }
            foreach (string name in duplicateCakes) { result.Cakes.Remove(name); result.Warnings.Add("重複 CAKE：" + name); }
            if (root.Elements("CAKE").Count() > 10000 || root.Elements("CREAM").Count() > 100000) throw new InvalidDataException("EFF definition count exceeds preview limits.");
            return result;
        }
        public byte[] WithMaterialReference(string reference)
        {
            if (reference == MaterialReference) return (byte[])Original.Clone();
            using (var input = new MemoryStream(Original))
            using (var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
            {
                var xml = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
                xml.Root.SetAttributeValue("NoriFileName", reference);
                using (var output = new MemoryStream())
                { using (var writer = XmlWriter.Create(output, new XmlWriterSettings { Encoding = new System.Text.UTF8Encoding(false), Indent = false, OmitXmlDeclaration = true, NewLineHandling = NewLineHandling.None })) xml.Save(writer); return output.ToArray(); }
            }
        }
    }
}
