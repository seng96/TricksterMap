using System;
using System.IO;
using TricksterMap;
using TricksterMap.Data;

namespace TricksterMap.Cli
{
    public sealed class CliApplication
    {
        private readonly MapDocumentService documentService = new MapDocumentService();
        private readonly PointObjectService pointObjectService = new PointObjectService();
        private readonly RangeObjectService rangeObjectService = new RangeObjectService();

        public int Run(string[] args, TextWriter output, TextWriter error)
        {
            if (args == null || args.Length == 0)
            {
                error.WriteLine("No command specified.");
                WriteUsage(error);
                return 1;
            }

            try
            {
                switch (args[0].ToLowerInvariant())
                {
                    case "inspect":
                        return RunInspect(args, output, error);
                    case "point":
                        return RunPoint(args, output, error);
                    case "range":
                        return RunRange(args, output, error);
                    default:
                        error.WriteLine("Unknown command: {0}", args[0]);
                        WriteUsage(error);
                        return 1;
                }
            }
            catch (Exception ex)
            {
                error.WriteLine(ex.Message);
                return 1;
            }
        }

        private int RunInspect(string[] args, TextWriter output, TextWriter error)
        {
            var filePath = GetRequiredValue(args, "--file");
            if (string.IsNullOrWhiteSpace(filePath))
            {
                error.WriteLine("Missing required option: --file");
                return 1;
            }

            var document = documentService.Load(filePath);
            var map = document.Map;

            output.WriteLine("File: {0}", Path.GetFileName(filePath));
            output.WriteLine("MapSizeX: {0}", map.MapSizeX);
            output.WriteLine("MapSizeY: {0}", map.MapSizeY);
            output.WriteLine("ConfigLayers: {0}", map.ConfigLayers.Count);
            output.WriteLine("PointObjects: {0}", map.PointObjects.Count);
            output.WriteLine("RangeObjects: {0}", map.RangeObjects.Count);
            return 0;
        }

        private int RunPoint(string[] args, TextWriter output, TextWriter error)
        {
            if (args.Length < 2)
            {
                error.WriteLine("Missing point subcommand.");
                WriteUsage(error);
                return 1;
            }

            var filePath = GetRequiredValue(args, "--file");
            if (string.IsNullOrWhiteSpace(filePath))
            {
                error.WriteLine("Missing required option: --file");
                return 1;
            }

            var document = documentService.Load(filePath);
            switch (args[1].ToLowerInvariant())
            {
                case "add":
                    pointObjectService.Add(document, CreatePoint(args));
                    documentService.Save(document);
                    output.WriteLine("Point added.");
                    return 0;
                case "update":
                    pointObjectService.Update(document, GetRequiredInt(args, "--index"), CreatePoint(args));
                    documentService.Save(document);
                    output.WriteLine("Point updated.");
                    return 0;
                case "delete":
                    pointObjectService.Delete(document, GetRequiredInt(args, "--index"));
                    documentService.Save(document);
                    output.WriteLine("Point deleted.");
                    return 0;
                default:
                    error.WriteLine("Unknown point subcommand: {0}", args[1]);
                    return 1;
            }
        }

        private static PointObject CreatePoint(string[] args)
        {
            return new PointObject
            {
                Type = GetRequiredInt(args, "--type"),
                Id = GetRequiredInt(args, "--id"),
                MapId = GetRequiredInt(args, "--map-id"),
                X = GetRequiredInt(args, "--x"),
                Y = GetRequiredInt(args, "--y")
            };
        }

        private int RunRange(string[] args, TextWriter output, TextWriter error)
        {
            if (args.Length < 2)
            {
                error.WriteLine("Missing range subcommand.");
                WriteUsage(error);
                return 1;
            }

            var filePath = GetRequiredValue(args, "--file");
            if (string.IsNullOrWhiteSpace(filePath))
            {
                error.WriteLine("Missing required option: --file");
                return 1;
            }

            var document = documentService.Load(filePath);
            switch (args[1].ToLowerInvariant())
            {
                case "add":
                    rangeObjectService.Add(document, CreateRange(args));
                    documentService.Save(document);
                    output.WriteLine("Range added.");
                    return 0;
                case "update":
                    rangeObjectService.Update(document, GetRequiredInt(args, "--index"), CreateRange(args));
                    documentService.Save(document);
                    output.WriteLine("Range updated.");
                    return 0;
                case "delete":
                    rangeObjectService.Delete(document, GetRequiredInt(args, "--index"));
                    documentService.Save(document);
                    output.WriteLine("Range deleted.");
                    return 0;
                default:
                    error.WriteLine("Unknown range subcommand: {0}", args[1]);
                    return 1;
            }
        }

        private static RangeObject CreateRange(string[] args)
        {
            return new RangeObject
            {
                Type = GetRequiredInt(args, "--type"),
                Id = GetRequiredInt(args, "--id"),
                Destination = GetRequiredInt(args, "--destination"),
                X1 = GetRequiredInt(args, "--x1"),
                Y1 = GetRequiredInt(args, "--y1"),
                X2 = GetRequiredInt(args, "--x2"),
                Y2 = GetRequiredInt(args, "--y2")
            };
        }

        private static string? GetRequiredValue(string[] args, string name)
        {
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return null;
        }

        private static int GetRequiredInt(string[] args, string name)
        {
            var value = GetRequiredValue(args, name);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(string.Format("Missing required option: {0}", name));
            }

            return int.Parse(value);
        }

        private static void WriteUsage(TextWriter writer)
        {
            writer.WriteLine("Usage:");
            writer.WriteLine("  inspect --file <path-to-md3>");
            writer.WriteLine("  point add --file <path> --type <id> --id <id> --map-id <id> --x <value> --y <value>");
            writer.WriteLine("  point update --file <path> --index <value> --type <id> --id <id> --map-id <id> --x <value> --y <value>");
            writer.WriteLine("  point delete --file <path> --index <value>");
            writer.WriteLine("  range add --file <path> --type <id> --id <id> --destination <id> --x1 <value> --y1 <value> --x2 <value> --y2 <value>");
            writer.WriteLine("  range update --file <path> --index <value> --type <id> --id <id> --destination <id> --x1 <value> --y1 <value> --x2 <value> --y2 <value>");
            writer.WriteLine("  range delete --file <path> --index <value>");
        }
    }
}
