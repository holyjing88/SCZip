using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using SCZip.Core;
using SCZip.Domain;
using SCZip.Infrastructure;
using SCZip.Services;
using UnityEngine;

namespace SCZip.Infrastructure.Testing
{
    /// <summary>Shared create/list/extract verification for all archive formats.</summary>
    public static class ArchiveRoundtripHarness
    {
        public readonly struct Case
        {
            public Case(ArchiveFormat format, bool expectCreate, string expectedEntryName, bool singleFileOnly = false)
            {
                Format = format;
                ExpectCreate = expectCreate;
                ExpectedEntryName = expectedEntryName;
                SingleFileOnly = singleFileOnly;
            }

            public ArchiveFormat Format { get; }
            public bool ExpectCreate { get; }
            public string ExpectedEntryName { get; }
            public bool SingleFileOnly { get; }
        }

        public static IReadOnlyList<Case> GetCases() => new[]
        {
            new Case(ArchiveFormat.Zip, true, "payload.txt"),
            new Case(ArchiveFormat.SevenZip, true, "payload.txt"),
            new Case(ArchiveFormat.TarGzip, true, "payload.txt"),
            new Case(ArchiveFormat.TarBzip2, true, "payload.txt"),
            new Case(ArchiveFormat.TarZstd, true, "payload.txt"),
            new Case(ArchiveFormat.Gzip, true, "payload.txt", singleFileOnly: true),
            new Case(ArchiveFormat.Bzip2, true, "payload.txt", singleFileOnly: true),
            new Case(ArchiveFormat.Zstd, true, "payload.txt", singleFileOnly: true),
            new Case(ArchiveFormat.TarXz, false, "payload.txt"),
            new Case(ArchiveFormat.Xz, false, "payload.txt", singleFileOnly: true),
            new Case(ArchiveFormat.Rar, false, "payload.txt")
        };

        public static void RunAll(string rootDirectory)
        {
            AppServices.EnsureInitialized();
            Directory.CreateDirectory(rootDirectory);

            foreach (var testCase in GetCases())
                RunCreateRoundtrip(rootDirectory, testCase);

            RunRarExtractFixture(rootDirectory);
            RunTarXzExtractFixture(rootDirectory);
            RunFormatDetection();
        }

        public static void RunCreateRoundtrip(string rootDirectory, Case testCase)
        {
            var label = testCase.Format.ToString();
            var workDir = Path.Combine(rootDirectory, "_roundtrip_" + label.ToLowerInvariant());
            if (Directory.Exists(workDir))
                Directory.Delete(workDir, true);
            Directory.CreateDirectory(workDir);

            var payloadPath = Path.Combine(workDir, "payload.txt");
            const string payload = "sczip-roundtrip";
            File.WriteAllText(payloadPath, payload);

            var subDir = Path.Combine(workDir, "folder");
            Directory.CreateDirectory(subDir);
            File.WriteAllText(Path.Combine(subDir, "nested.txt"), "nested");

            var sourcePaths = testCase.SingleFileOnly
                ? new[] { payloadPath }
                : new[] { payloadPath, subDir };

            var ext = ArchiveFormatRegistry.GetDefaultExtension(testCase.Format);
            var archiveBaseName = testCase.SingleFileOnly ? testCase.ExpectedEntryName : "test";
            var archivePath = Path.Combine(workDir, archiveBaseName + ext);
            var extractDir = Path.Combine(workDir, "out");

            var provider = AppServices.Archive.GetProvider(testCase.Format);
            if (provider == null)
                throw new InvalidOperationException($"{label}: no provider registered");

            if (!testCase.ExpectCreate)
            {
                if (provider.CanCreate)
                    throw new InvalidOperationException($"{label}: expected CanCreate=false");

                try
                {
                    AppServices.Archive.CreateAsync(new CreateArchiveOptions
                    {
                        Format = testCase.Format,
                        OutputPath = archivePath,
                        SourcePaths = sourcePaths
                    }, null, CancellationToken.None).GetAwaiter().GetResult();
                    throw new InvalidOperationException($"{label}: create should have failed");
                }
                catch (NotSupportedException)
                {
                    return;
                }
            }

            if (!provider.CanCreate)
                throw new InvalidOperationException($"{label}: provider cannot create");

            AppServices.Archive.CreateAsync(new CreateArchiveOptions
            {
                Format = testCase.Format,
                OutputPath = archivePath,
                SourcePaths = sourcePaths,
                Level = ArchiveCompressionLevel.Normal
            }, null, CancellationToken.None).GetAwaiter().GetResult();

            if (!File.Exists(archivePath))
                throw new InvalidOperationException($"{label}: archive not created");

            var list = AppServices.Archive.ListEntriesAsync(archivePath, new ArchiveOpenOptions(), CancellationToken.None)
                .GetAwaiter().GetResult();
            if (list.Count == 0)
                throw new InvalidOperationException($"{label}: list entries empty");

            Directory.CreateDirectory(extractDir);
            AppServices.Archive.ExtractAsync(new ExtractOptions
            {
                ArchivePath = archivePath,
                DestinationDirectory = extractDir
            }, null, CancellationToken.None).GetAwaiter().GetResult();

            var expected = Path.Combine(extractDir, testCase.ExpectedEntryName);
            if (!File.Exists(expected))
                throw new InvalidOperationException($"{label}: missing extracted file '{testCase.ExpectedEntryName}'");

            if (File.ReadAllText(expected) != payload)
                throw new InvalidOperationException($"{label}: payload mismatch");
        }

        public static void RunRarExtractFixture(string rootDirectory)
        {
            var fixture = ResolveFixturePath("Rar4.rar");
            if (fixture == null)
                return;

            ExtractFixture(rootDirectory, "_roundtrip_rar_fixture", fixture, ArchiveFormat.Rar);
        }

        public static void RunTarXzExtractFixture(string rootDirectory)
        {
            var fixture = ResolveFixturePath("Tar.tar.xz");
            if (fixture == null)
                return;

            ExtractFixture(rootDirectory, "_roundtrip_tarxz_fixture", fixture, ArchiveFormat.TarXz);
        }

        public static void RunFormatDetection()
        {
            void AssertFmt(string path, ArchiveFormat expected)
            {
                var fmt = AppServices.Archive.DetectFormat(path);
                if (fmt != expected)
                    throw new InvalidOperationException($"DetectFormat({path}) expected {expected}, got {fmt}");
            }

            AssertFmt("a.zip", ArchiveFormat.Zip);
            AssertFmt("a.7z", ArchiveFormat.SevenZip);
            AssertFmt("a.rar", ArchiveFormat.Rar);
            AssertFmt("a.tar.gz", ArchiveFormat.TarGzip);
            AssertFmt("a.tgz", ArchiveFormat.TarGzip);
            AssertFmt("a.tar.bz2", ArchiveFormat.TarBzip2);
            AssertFmt("a.tar.xz", ArchiveFormat.TarXz);
            AssertFmt("a.tar.zst", ArchiveFormat.TarZstd);
            AssertFmt("a.gz", ArchiveFormat.Gzip);
            AssertFmt("a.bz2", ArchiveFormat.Bzip2);
            AssertFmt("a.xz", ArchiveFormat.Xz);
            AssertFmt("a.zst", ArchiveFormat.Zstd);
        }

        private static void ExtractFixture(string rootDirectory, string workName, string fixturePath, ArchiveFormat format)
        {
            var workDir = Path.Combine(rootDirectory, workName);
            if (Directory.Exists(workDir))
                Directory.Delete(workDir, true);
            Directory.CreateDirectory(workDir);

            var extractDir = Path.Combine(workDir, "out");
            Directory.CreateDirectory(extractDir);

            if (AppServices.Archive.DetectFormat(fixturePath) != format)
                throw new InvalidOperationException($"{format} fixture has unexpected format: {fixturePath}");

            var list = AppServices.Archive.ListEntriesAsync(fixturePath, new ArchiveOpenOptions(), CancellationToken.None)
                .GetAwaiter().GetResult();
            if (list.Count == 0)
                throw new InvalidOperationException($"{format} fixture: list empty");

            AppServices.Archive.ExtractAsync(new ExtractOptions
            {
                ArchivePath = fixturePath,
                DestinationDirectory = extractDir
            }, null, CancellationToken.None).GetAwaiter().GetResult();

            if (!Directory.EnumerateFiles(extractDir, "*", SearchOption.AllDirectories).Any())
                throw new InvalidOperationException($"{format} fixture: extract produced no files");
        }

        private static string ResolveFixturePath(string fileName)
        {
            var dataPath = Application.dataPath;
            var direct = Path.Combine(dataPath, "_SCZip/Tests/Fixtures", fileName);
            if (File.Exists(direct))
                return direct;

            var fallback = Path.Combine(Directory.GetCurrentDirectory(), "Assets/_SCZip/Tests/Fixtures", fileName);
            return File.Exists(fallback) ? Path.GetFullPath(fallback) : null;
        }
    }
}
