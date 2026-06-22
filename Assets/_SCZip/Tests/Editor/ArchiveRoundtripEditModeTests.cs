using System.IO;
using NUnit.Framework;
using SCZip.Core;
using SCZip.Domain;
using SCZip.Infrastructure.Testing;
using UnityEngine;

namespace SCZip.Tests.EditMode
{
    public sealed class ArchiveRoundtripEditModeTests
    {
        private string _workRoot;

        [SetUp]
        public void SetUp()
        {
            AppServices.EnsureInitialized();
            _workRoot = Path.Combine(Application.temporaryCachePath, "SCZipArchiveTests");
            if (Directory.Exists(_workRoot))
                Directory.Delete(_workRoot, true);
            Directory.CreateDirectory(_workRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_workRoot))
                Directory.Delete(_workRoot, true);
        }

        [Test]
        public void Format_detection_covers_all_extensions()
        {
            Assert.DoesNotThrow(() => ArchiveRoundtripHarness.RunFormatDetection());
        }

        [TestCase(ArchiveFormat.Zip)]
        [TestCase(ArchiveFormat.SevenZip)]
        [TestCase(ArchiveFormat.TarGzip)]
        [TestCase(ArchiveFormat.TarBzip2)]
        [TestCase(ArchiveFormat.TarZstd)]
        [TestCase(ArchiveFormat.Gzip)]
        [TestCase(ArchiveFormat.Bzip2)]
        [TestCase(ArchiveFormat.Zstd)]
        public void Create_list_extract_roundtrip(ArchiveFormat format)
        {
            var testCase = FindCase(format);
            Assert.IsTrue(testCase.ExpectCreate, $"{format} should support create");
            Assert.DoesNotThrow(() => ArchiveRoundtripHarness.RunCreateRoundtrip(_workRoot, testCase));
        }

        [TestCase(ArchiveFormat.Rar)]
        [TestCase(ArchiveFormat.Xz)]
        [TestCase(ArchiveFormat.TarXz)]
        public void Create_is_not_supported(ArchiveFormat format)
        {
            var testCase = FindCase(format);
            Assert.IsFalse(testCase.ExpectCreate, $"{format} should not support create");
            Assert.DoesNotThrow(() => ArchiveRoundtripHarness.RunCreateRoundtrip(_workRoot, testCase));
        }

        [Test]
        public void Rar_fixture_extracts()
        {
            var fixture = Path.Combine(Application.dataPath, "_SCZip/Tests/Fixtures/Rar4.rar");
            if (!File.Exists(fixture))
            {
                Assert.Ignore("RAR fixture missing: " + fixture);
                return;
            }

            Assert.DoesNotThrow(() => ArchiveRoundtripHarness.RunRarExtractFixture(_workRoot));
        }

        [Test]
        public void TarXz_fixture_extracts()
        {
            var fixture = Path.Combine(Application.dataPath, "_SCZip/Tests/Fixtures/Tar.tar.xz");
            if (!File.Exists(fixture))
            {
                Assert.Ignore("TAR.XZ fixture missing: " + fixture);
                return;
            }

            Assert.DoesNotThrow(() => ArchiveRoundtripHarness.RunTarXzExtractFixture(_workRoot));
        }

        [Test]
        public void All_creatable_formats_are_registered()
        {
            AppServices.EnsureInitialized();
            foreach (var format in AppServices.Archive.GetCreatableFormats())
            {
                var provider = AppServices.Archive.GetProvider(format);
                Assert.NotNull(provider, $"Missing provider for {format}");
                Assert.IsTrue(provider.CanCreate, $"{format} listed as creatable");
            }
        }

        private static ArchiveRoundtripHarness.Case FindCase(ArchiveFormat format)
        {
            foreach (var testCase in ArchiveRoundtripHarness.GetCases())
            {
                if (testCase.Format == format)
                    return testCase;
            }

            Assert.Fail($"No roundtrip case for {format}");
            return default;
        }
    }
}
