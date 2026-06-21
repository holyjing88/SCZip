using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SCZip.Domain;
using SCZip.Services;

namespace SCZip.Infrastructure.Providers
{
    /// <summary>Minimal tar.gz read/write for MVP (ustar subset).</summary>
    public sealed class TarGzipArchiveProvider : IArchiveProvider
    {
        public ArchiveFormat Format => ArchiveFormat.TarGzip;
        public bool CanList => true;
        public bool CanExtract => true;
        public bool CanCreate => true;
        public bool SupportsEncryption => false;

        public Task<IReadOnlyList<ArchiveEntry>> ListEntriesAsync(string path, ArchiveOpenOptions options,
            CancellationToken ct)
        {
            return Task.Run(() =>
            {
                var entries = ReadTar(ReadGzip(path));
                return (IReadOnlyList<ArchiveEntry>)entries
                    .OrderByDescending(e => e.IsDirectory)
                    .ThenBy(e => e.Name)
                    .ToList();
            }, ct);
        }

        public Task ExtractAsync(ExtractOptions options, IProgress<ArchiveProgress> progress, CancellationToken ct)
        {
            return Task.Run(() =>
            {
                var all = ReadTar(ReadGzip(options.ArchivePath));
                var toExtract = all.Where(e => !e.IsDirectory).ToList();
                if (options.EntryPaths != null && options.EntryPaths.Count > 0)
                {
                    var set = new HashSet<string>(options.EntryPaths);
                    toExtract = toExtract.Where(e => set.Contains(e.Path)).ToList();
                }

                var data = ReadTarWithData(ReadGzip(options.ArchivePath));
                var total = Math.Max(toExtract.Count, 1);
                for (var i = 0; i < toExtract.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var entry = toExtract[i];
                    if (!data.TryGetValue(entry.Path, out var bytes)) continue;
                    var dest = Path.Combine(options.DestinationDirectory, entry.Path.Replace('/', Path.DirectorySeparatorChar));
                    var dir = Path.GetDirectoryName(dest);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                    File.WriteAllBytes(dest, bytes);
                    progress?.Report(new ArchiveProgress { Progress = (i + 1) / (float)total, CurrentItem = entry.Name });
                }
            }, ct);
        }

        public Task CreateAsync(CreateArchiveOptions options, IProgress<ArchiveProgress> progress, CancellationToken ct)
        {
            return Task.Run(() =>
            {
                var files = new List<(string name, byte[] data)>();
                foreach (var src in options.SourcePaths ?? Array.Empty<string>())
                {
                    if (File.Exists(src))
                        files.Add((Path.GetFileName(src), File.ReadAllBytes(src)));
                    else if (Directory.Exists(src))
                    {
                        foreach (var f in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
                        {
                            var rel = f.Substring(src.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                .Replace('\\', '/');
                            files.Add((rel, File.ReadAllBytes(f)));
                        }
                    }
                }

                var tar = WriteTar(files);
                WriteGzip(options.OutputPath, tar);
                progress?.Report(new ArchiveProgress { Progress = 1f, CurrentItem = Path.GetFileName(options.OutputPath) });
            }, ct);
        }

        private static byte[] ReadGzip(string path)
        {
            using var fs = File.OpenRead(path);
            using var gz = new GZipStream(fs, CompressionMode.Decompress);
            using var ms = new MemoryStream();
            gz.CopyTo(ms);
            return ms.ToArray();
        }

        private static void WriteGzip(string path, byte[] data)
        {
            using var fs = File.Create(path);
            using var gz = new GZipStream(fs, System.IO.Compression.CompressionLevel.Optimal);
            gz.Write(data, 0, data.Length);
        }

        private static List<ArchiveEntry> ReadTar(byte[] tar)
        {
            var list = new List<ArchiveEntry>();
            var offset = 0;
            while (offset + 512 <= tar.Length)
            {
                var block = new byte[512];
                Array.Copy(tar, offset, block, 0, 512);
                if (block.All(b => b == 0)) break;

                var name = ReadCString(block, 0, 100);
                var sizeOctal = ReadCString(block, 124, 12);
                var type = block[156];
                var size = Convert.ToInt64(sizeOctal.Trim(), 8);
                var isDir = type == '5' || type == (byte)'0' && size == 0 && name.EndsWith("/");

                if (!string.IsNullOrEmpty(name))
                {
                    list.Add(new ArchiveEntry
                    {
                        Path = name.TrimEnd('/'),
                        Name = name.TrimEnd('/').Split('/').Last(),
                        IsDirectory = isDir,
                        SizeBytes = size
                    });
                }

                offset += 512;
                var padded = (int)((size + 511) / 512) * 512;
                offset += padded;
            }

            return list;
        }

        private static Dictionary<string, byte[]> ReadTarWithData(byte[] tar)
        {
            var dict = new Dictionary<string, byte[]>();
            var offset = 0;
            while (offset + 512 <= tar.Length)
            {
                var block = new byte[512];
                Array.Copy(tar, offset, block, 0, 512);
                if (block.All(b => b == 0)) break;

                var name = ReadCString(block, 0, 100).TrimEnd('/');
                var sizeOctal = ReadCString(block, 124, 12);
                var type = block[156];
                var size = Convert.ToInt64(sizeOctal.Trim(), 8);
                offset += 512;

                if (type != '5' && size > 0 && !string.IsNullOrEmpty(name))
                {
                    var data = new byte[size];
                    Array.Copy(tar, offset, data, 0, size);
                    dict[name] = data;
                }

                var padded = (int)((size + 511) / 512) * 512;
                offset += padded;
            }

            return dict;
        }

        private static byte[] WriteTar(List<(string name, byte[] data)> files)
        {
            using var ms = new MemoryStream();
            foreach (var (name, data) in files)
            {
                var header = new byte[512];
                WriteCString(header, 0, name, 100);
                WriteOctal(header, 124, data.LongLength, 12);
                header[156] = (byte)'0';
                Encoding.ASCII.GetBytes("ustar").CopyTo(header, 257);
                var checksum = 0;
                for (var i = 0; i < 512; i++) checksum += header[i];
                WriteOctal(header, 148, checksum, 8);
                ms.Write(header, 0, 512);
                ms.Write(data, 0, data.Length);
                var pad = (512 - (data.Length % 512)) % 512;
                if (pad > 0) ms.Write(new byte[pad], 0, pad);
            }

            var endBlocks = new byte[1024];
            ms.Write(endBlocks, 0, endBlocks.Length);
            return ms.ToArray();
        }

        private static string ReadCString(byte[] buf, int offset, int len)
        {
            var end = offset;
            while (end < offset + len && buf[end] != 0) end++;
            return Encoding.ASCII.GetString(buf, offset, end - offset);
        }

        private static void WriteCString(byte[] buf, int offset, string s, int max)
        {
            var bytes = Encoding.ASCII.GetBytes(s);
            Array.Copy(bytes, 0, buf, offset, Math.Min(bytes.Length, max - 1));
        }

        private static void WriteOctal(byte[] buf, int offset, long value, int len)
        {
            var s = Convert.ToString(value, 8).PadLeft(len - 1, '0') + "\0";
            var bytes = Encoding.ASCII.GetBytes(s);
            Array.Copy(bytes, 0, buf, offset, Math.Min(bytes.Length, len));
        }
    }
}
