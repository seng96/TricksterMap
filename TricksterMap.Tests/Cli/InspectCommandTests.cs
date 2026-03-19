using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using TricksterMap.Data;
using Xunit;

namespace TricksterMap.Tests.Cli
{
    public class InspectCommandTests
    {
        [Fact]
        public void Inspect_existing_map_outputs_summary()
        {
            using (var fixture = TestMapFixture.Create())
            {
                var result = CliRunner.Run("inspect", "--file", fixture.Md3Path);

                Assert.Equal(0, result.ExitCode);
                Assert.Contains("MapSizeX: 512", result.StdOut);
                Assert.Contains("MapSizeY: 256", result.StdOut);
                Assert.Contains("PointObjects: 1", result.StdOut);
                Assert.Contains("RangeObjects: 1", result.StdOut);
            }
        }

        [Fact]
        public void Inspect_missing_map_returns_error()
        {
            var missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.md3");

            var result = CliRunner.Run("inspect", "--file", missingPath);

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("does not exist", result.StdErr + result.StdOut, StringComparison.OrdinalIgnoreCase);
        }
    }

    public class PointCommandTests
    {
        [Fact]
        public void Point_add_appends_new_point()
        {
            using (var fixture = TestMapFixture.Create())
            {
                var result = CliRunner.Run(
                    "point",
                    "add",
                    "--file",
                    fixture.Md3Path,
                    "--type",
                    "4",
                    "--id",
                    "22",
                    "--map-id",
                    "0",
                    "--x",
                    "321",
                    "--y",
                    "654");

                Assert.Equal(0, result.ExitCode);

                var document = new MapDocumentService().Load(fixture.Md3Path);
                Assert.Equal(2, document.Map.PointObjects.Count);
                Assert.Equal(22, document.Map.PointObjects[1].Id);
                Assert.Equal(4, document.Map.PointObjects[1].Type);
                Assert.Equal(321, document.Map.PointObjects[1].X);
                Assert.Equal(654, document.Map.PointObjects[1].Y);
            }
        }

        [Fact]
        public void Point_update_replaces_existing_point()
        {
            using (var fixture = TestMapFixture.Create())
            {
                var result = CliRunner.Run(
                    "point",
                    "update",
                    "--file",
                    fixture.Md3Path,
                    "--index",
                    "0",
                    "--type",
                    "5",
                    "--id",
                    "11",
                    "--map-id",
                    "3",
                    "--x",
                    "12",
                    "--y",
                    "34");

                Assert.Equal(0, result.ExitCode);

                var document = new MapDocumentService().Load(fixture.Md3Path);
                Assert.Single(document.Map.PointObjects);
                Assert.Equal(11, document.Map.PointObjects[0].Id);
                Assert.Equal(5, document.Map.PointObjects[0].Type);
                Assert.Equal(3, document.Map.PointObjects[0].MapId);
                Assert.Equal(12, document.Map.PointObjects[0].X);
                Assert.Equal(34, document.Map.PointObjects[0].Y);
            }
        }

        [Fact]
        public void Point_delete_removes_existing_point()
        {
            using (var fixture = TestMapFixture.Create())
            {
                var result = CliRunner.Run(
                    "point",
                    "delete",
                    "--file",
                    fixture.Md3Path,
                    "--index",
                    "0");

                Assert.Equal(0, result.ExitCode);

                var document = new MapDocumentService().Load(fixture.Md3Path);
                Assert.Empty(document.Map.PointObjects);
            }
        }

        [Fact]
        public void Point_add_rejects_duplicate_ids_for_same_type()
        {
            using (var fixture = TestMapFixture.Create())
            {
                var result = CliRunner.Run(
                    "point",
                    "add",
                    "--file",
                    fixture.Md3Path,
                    "--type",
                    "3",
                    "--id",
                    "10",
                    "--map-id",
                    "0",
                    "--x",
                    "321",
                    "--y",
                    "654");

                Assert.NotEqual(0, result.ExitCode);
                Assert.Contains("Duplicate ID", result.StdErr + result.StdOut, StringComparison.OrdinalIgnoreCase);

                var document = new MapDocumentService().Load(fixture.Md3Path);
                Assert.Single(document.Map.PointObjects);
            }
        }

        [Fact]
        public void Point_add_fails_when_companion_file_is_missing()
        {
            using (var fixture = TestMapFixture.Create())
            {
                File.Delete(Path.ChangeExtension(fixture.Md3Path, ".bac"));

                var result = CliRunner.Run(
                    "point",
                    "add",
                    "--file",
                    fixture.Md3Path,
                    "--type",
                    "4",
                    "--id",
                    "22",
                    "--map-id",
                    "0",
                    "--x",
                    "321",
                    "--y",
                    "654");

                Assert.NotEqual(0, result.ExitCode);
                Assert.Contains("Required companion file is missing", result.StdErr + result.StdOut, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    public class RangeCommandTests
    {
        [Fact]
        public void Range_add_appends_new_range()
        {
            using (var fixture = TestMapFixture.Create())
            {
                var result = CliRunner.Run(
                    "range",
                    "add",
                    "--file",
                    fixture.Md3Path,
                    "--type",
                    "6",
                    "--id",
                    "9",
                    "--destination",
                    "123",
                    "--x1",
                    "11",
                    "--y1",
                    "22",
                    "--x2",
                    "33",
                    "--y2",
                    "44");

                Assert.Equal(0, result.ExitCode);

                var document = new MapDocumentService().Load(fixture.Md3Path);
                Assert.Equal(2, document.Map.RangeObjects.Count);
                Assert.Equal(9, document.Map.RangeObjects[1].Id);
                Assert.Equal(6, document.Map.RangeObjects[1].Type);
                Assert.Equal(123, document.Map.RangeObjects[1].Destination);
                Assert.Equal(11, document.Map.RangeObjects[1].X1);
                Assert.Equal(44, document.Map.RangeObjects[1].Y2);
            }
        }

        [Fact]
        public void Range_update_replaces_existing_range()
        {
            using (var fixture = TestMapFixture.Create())
            {
                var result = CliRunner.Run(
                    "range",
                    "update",
                    "--file",
                    fixture.Md3Path,
                    "--index",
                    "0",
                    "--type",
                    "5",
                    "--id",
                    "88",
                    "--destination",
                    "456",
                    "--x1",
                    "100",
                    "--y1",
                    "101",
                    "--x2",
                    "200",
                    "--y2",
                    "201");

                Assert.Equal(0, result.ExitCode);

                var document = new MapDocumentService().Load(fixture.Md3Path);
                Assert.Single(document.Map.RangeObjects);
                Assert.Equal(88, document.Map.RangeObjects[0].Id);
                Assert.Equal(5, document.Map.RangeObjects[0].Type);
                Assert.Equal(456, document.Map.RangeObjects[0].Destination);
                Assert.Equal(100, document.Map.RangeObjects[0].X1);
                Assert.Equal(201, document.Map.RangeObjects[0].Y2);
            }
        }

        [Fact]
        public void Range_delete_removes_existing_range()
        {
            using (var fixture = TestMapFixture.Create())
            {
                var result = CliRunner.Run(
                    "range",
                    "delete",
                    "--file",
                    fixture.Md3Path,
                    "--index",
                    "0");

                Assert.Equal(0, result.ExitCode);

                var document = new MapDocumentService().Load(fixture.Md3Path);
                Assert.Empty(document.Map.RangeObjects);
            }
        }
    }

    internal sealed class TestMapFixture : IDisposable
    {
        private TestMapFixture(string directory, string md3Path)
        {
            DirectoryPath = directory;
            Md3Path = md3Path;
        }

        public string DirectoryPath { get; }

        public string Md3Path { get; }

        public static TestMapFixture Create()
        {
            var directory = Path.Combine(Path.GetTempPath(), "TricksterMap.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);

            var md3Path = Path.Combine(directory, "sample.md3");
            var map = new MapDataInfo
            {
                Version = 0x012E,
                TileBpp = 16,
                MapSizeX = 512,
                MapSizeY = 256,
                TileSizeX = 32,
                TileSizeY = 32,
                TileCountX = 16,
                TileCountY = 8,
                BacFileSize = 12,
                TilFileSize = 16,
                LyrFileSize = 20
            };

            map.PointObjects.Add(new PointObject
            {
                Id = 10,
                Type = 0x03,
                MapId = 0,
                X = 120,
                Y = 180
            });

            map.RangeObjects.Add(new RangeObject
            {
                Id = 7,
                Type = 0x09,
                Destination = 0,
                X1 = 10,
                Y1 = 20,
                X2 = 30,
                Y2 = 40
            });

            File.WriteAllBytes(Path.ChangeExtension(md3Path, ".bac"), new byte[12]);
            File.WriteAllBytes(Path.ChangeExtension(md3Path, ".til"), new byte[16]);
            File.WriteAllBytes(Path.ChangeExtension(md3Path, ".lyr"), new byte[20]);

            MapSaveHelper.Save(md3Path, map);

            return new TestMapFixture(directory, md3Path);
        }

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, true);
            }
        }
    }

    internal static class CliRunner
    {
        public static CliRunResult Run(params string[] args)
        {
            var cliDllPath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "TricksterMap.Cli",
                "bin",
                "Debug",
                "net10.0",
                "TricksterMap.Cli.dll"));

            var psi = new ProcessStartInfo("dotnet")
            {
                Arguments = BuildArguments(cliDllPath, args),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(psi))
            {
                Assert.NotNull(process);
                var stdOut = process.StandardOutput.ReadToEnd();
                var stdErr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                return new CliRunResult(process.ExitCode, stdOut, stdErr);
            }
        }

        private static string BuildArguments(string cliDllPath, string[] args)
        {
            var builder = new StringBuilder();
            builder.Append('"').Append(cliDllPath).Append('"');

            foreach (var arg in args)
            {
                builder.Append(' ')
                    .Append('"')
                    .Append(arg.Replace("\"", "\\\""))
                    .Append('"');
            }

            return builder.ToString();
        }
    }

    public class BundledSkillWrapperTests
    {
        [Fact]
        public void Bundled_wrapper_runs_published_cli()
        {
            var publishScript = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                ".codex",
                "skills",
                "md3-cli-automation",
                "scripts",
                "publish-cli.ps1"));

            var wrapperScript = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                ".codex",
                "skills",
                "md3-cli-automation",
                "scripts",
                "run-md3-cli.ps1"));

            var publishResult = ScriptRunner.RunPowerShellFile(publishScript);
            Assert.Equal(0, publishResult.ExitCode);

            var wrapperResult = ScriptRunner.RunPowerShellFile(
                wrapperScript,
                "inspect",
                "--file",
                @"C:\nonexistent\missing.md3");

            Assert.NotEqual(0, wrapperResult.ExitCode);
            Assert.Contains("does not exist", wrapperResult.StdErr + wrapperResult.StdOut, StringComparison.OrdinalIgnoreCase);
        }
    }

    internal static class ScriptRunner
    {
        public static CliRunResult RunPowerShellFile(string scriptPath, params string[] args)
        {
            var psi = new ProcessStartInfo("powershell")
            {
                Arguments = BuildScriptArguments(scriptPath, args),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(psi))
            {
                Assert.NotNull(process);
                var stdOut = process.StandardOutput.ReadToEnd();
                var stdErr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                return new CliRunResult(process.ExitCode, stdOut, stdErr);
            }
        }

        private static string BuildScriptArguments(string scriptPath, string[] args)
        {
            var builder = new StringBuilder();
            builder.Append("-ExecutionPolicy Bypass -File ")
                .Append('"')
                .Append(scriptPath)
                .Append('"');

            foreach (var arg in args)
            {
                builder.Append(' ')
                    .Append('"')
                    .Append(arg.Replace("\"", "\\\""))
                    .Append('"');
            }

            return builder.ToString();
        }
    }

    internal sealed class CliRunResult
    {
        public CliRunResult(int exitCode, string stdOut, string stdErr)
        {
            ExitCode = exitCode;
            StdOut = stdOut;
            StdErr = stdErr;
        }

        public int ExitCode { get; }

        public string StdOut { get; }

        public string StdErr { get; }
    }
}
