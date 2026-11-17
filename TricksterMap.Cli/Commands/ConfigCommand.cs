using System.Globalization;
using TricksterMap;

namespace TricksterMap.Cli.Commands;

/// <summary>Cell coordinates address the stored grid, not map pixels.</summary>
public static class ConfigCommand
{
    public static int Run(string[] args, TextWriter output)
    {
        if (args.Length < 2 || !new[] { "list", "set", "rect" }.Contains(args[1]))
            throw new ArgumentException("Use config list|set|rect. Coordinates are zero-based grid cells.");
        string operation = args[1];
        var allowed = new HashSet<string> { "--file" };
        if (operation != "list") allowed.UnionWith(new[] { "--layer", "--value" });
        if (operation == "set") allowed.UnionWith(new[] { "--x", "--y" });
        if (operation == "rect") allowed.UnionWith(new[] { "--x1", "--y1", "--x2", "--y2" });
        var options = new Dictionary<string, string>();
        for (int i = 2; i < args.Length; i += 2)
        {
            if (i + 1 >= args.Length || !allowed.Contains(args[i]) || !options.TryAdd(args[i], args[i + 1]))
                throw new ArgumentException("Unknown, repeated, or incomplete option: " + args[i]);
        }
        string Required(string key) => options.TryGetValue(key, out string? value)
            ? value : throw new ArgumentException("Missing required option: " + key);
        int Number(string key) => int.Parse(Required(key), CultureInfo.InvariantCulture);
        var documents = new MapDocumentService();
        var document = documents.Load(Required("--file"));
        if (operation == "list")
        {
            for (int index = 0; index < document.Map.ConfigLayers.Count; index++)
            {
                var layer = document.Map.ConfigLayers[index];
                output.WriteLine($"Layer {index}: Type={layer.Type} Grid={layer.X}x{layer.Y} Step={layer.BppX}x{layer.BppY} Bytes={layer.Data.Length}");
            }
            return 0;
        }
        int layerIndex = Number("--layer"), valueByte = Number("--value");
        if (layerIndex < 0 || layerIndex >= document.Map.ConfigLayers.Count)
            throw new ArgumentOutOfRangeException("--layer");
        if (valueByte < 0 || valueByte > 255) throw new ArgumentOutOfRangeException("--value", "Expected 0..255.");
        var edit = new ConfigLayerEditService(document.Map.ConfigLayers[layerIndex]);
        if (operation == "set") edit.SetCell(Number("--x"), Number("--y"), (byte)valueByte);
        else edit.FillRectangle(Number("--x1"), Number("--y1"), Number("--x2"), Number("--y2"), (byte)valueByte);
        if (edit.IsDirty) documents.Save(document);
        output.WriteLine(edit.IsDirty ? "Config grid saved." : "No cells changed.");
        return 0;
    }
}
