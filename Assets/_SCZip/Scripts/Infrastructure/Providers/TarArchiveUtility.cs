using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SCZip.Infrastructure.Providers
{
    internal static class TarArchiveUtility
    {
        public static List<Domain.ArchiveEntry> ReadEntries(byte[] tar) =>
            ReadTar(tar).OrderByDescending(e => e.IsDirectory).ThenBy(e => e.Name).ToList();

        public static Dictionary<string, byte[]> ReadFileData(byte[] tar)
        {
            var dict = new Dictionary<string, byte[]>();
            var offset = 0;
            while (offset + 512 <= tar.Length)
            {
                var block = new byte[512];
                Array.Copy(tar, offset, block, 0, 512);
                if (block.All(b => b == 0))
                    break;

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

        public static byte[] WriteTar(IReadOnlyList<ArchiveSourceFile> files)
        {
            using var ms = new MemoryStream();
            foreach (var file in files)
            {
                var data = File.ReadAllBytes(file.FullPath);
                var header = new byte[512];
                WriteCString(header, 0, file.Key, 100);
                WriteOctal(header, 124, data.LongLength, 12);
                header[156] = (byte)'0';
                Encoding.ASCII.GetBytes("ustar").CopyTo(header, 257);
                var checksum = 0;
                for (var i = 0; i < 512; i++)
                    checksum += header[i];
                WriteOctal(header, 148, checksum, 8);
                ms.Write(header, 0, 512);
                ms.Write(data, 0, data.Length);
                var pad = (512 - (data.Length % 512)) % 512;
                if (pad > 0)
                    ms.Write(new byte[pad], 0, pad);
            }

            ms.Write(new byte[1024], 0, 1024);
            return ms.ToArray();
        }

        private static List<Domain.ArchiveEntry> ReadTar(byte[] tar)
        {
            var list = new List<Domain.ArchiveEntry>();
            var offset = 0;
            while (offset + 512 <= tar.Length)
            {
                var block = new byte[512];
                Array.Copy(tar, offset, block, 0, 512);
                if (block.All(b => b == 0))
                    break;

                var name = ReadCString(block, 0, 100);
                var sizeOctal = ReadCString(block, 124, 12);
                var type = block[156];
                var size = Convert.ToInt64(sizeOctal.Trim(), 8);
                var isDir = type == '5' || type == (byte)'0' && size == 0 && name.EndsWith("/");

                if (!string.IsNullOrEmpty(name))
                {
                    list.Add(new Domain.ArchiveEntry
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

        private static string ReadCString(byte[] buf, int offset, int len)
        {
            var end = offset;
            while (end < offset + len && buf[end] != 0)
                end++;
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
