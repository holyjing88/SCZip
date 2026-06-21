using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SCZip.Services;
using UnityEngine;

namespace SCZip.Infrastructure.SystemRecent
{
    /// <summary>
    /// Reads macOS recently used files via Spotlight (kMDItemLastUsedDate),
    /// aligned with Finder "Recents" behavior.
    /// </summary>
    public sealed class MacSystemRecentProvider : ISystemRecentProvider
    {
        private const int ScanLimit = 250;
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

        private static IReadOnlyList<SystemRecentItem> _cache;
        private static DateTime _cacheExpiresUtc;

        public static void InvalidateCache()
        {
            _cache = null;
            _cacheExpiresUtc = default;
        }

        public IReadOnlyList<SystemRecentItem> GetRecentItems(int maxItems)
        {
            if (maxItems <= 0)
                return Array.Empty<SystemRecentItem>();

            if (_cache != null && DateTime.UtcNow < _cacheExpiresUtc)
                return _cache.Take(maxItems).ToList();

            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(home))
                return Array.Empty<SystemRecentItem>();

            var parsed = QuerySpotlightRecents(home, Math.Max(maxItems, ScanLimit));
            _cache = parsed;
            _cacheExpiresUtc = DateTime.UtcNow.Add(CacheTtl);
            return parsed.Take(maxItems).ToList();
        }

        private static List<SystemRecentItem> QuerySpotlightRecents(string home, int maxItems)
        {
            var script = BuildQueryScript(home, maxItems);
            var output = RunBash(script);
            if (string.IsNullOrWhiteSpace(output))
                return new List<SystemRecentItem>();

            var results = new List<SystemRecentItem>();
            foreach (var line in output.Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var tab = line.IndexOf('\t');
                if (tab <= 0)
                    continue;

                var dateText = line.Substring(0, tab).Trim();
                var path = line.Substring(tab + 1).Trim();
                if (string.IsNullOrEmpty(path))
                    continue;

                if (!DateTime.TryParse(dateText, CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                        out var lastUsedUtc))
                    continue;

                if (!File.Exists(path) && !Directory.Exists(path))
                    continue;

                results.Add(new SystemRecentItem
                {
                    Path = path,
                    LastUsedUtc = lastUsedUtc
                });
            }

            return results;
        }

        private static string BuildQueryScript(string home, int maxItems)
        {
            return string.Join("\n", new[]
            {
                "set -euo pipefail",
                "HOME_DIR=" + BashEscape(home),
                "MAX=" + maxItems,
                "mdfind -onlyin \"$HOME_DIR\" 'kMDItemLastUsedDate >= $time.today(-90)' 2>/dev/null | while IFS= read -r f; do",
                "  [ -e \"$f\" ] || continue",
                "  case \"$f\" in",
                "    *.app|*.app/*|/Applications/*|/System/*|*/Library/Containers/*|*/Library/Caches/*|*/.Trash/*) continue ;;",
                "  esac",
                "  base=\"${f##*/}\"",
                "  case \"$base\" in .*) continue ;; esac",
                "  d=$(mdls -raw -name kMDItemLastUsedDate \"$f\" 2>/dev/null || true)",
                "  [ \"$d\" = \"(null)\" ] && continue",
                "  [ -z \"$d\" ] && continue",
                "  printf '%s\\t%s\\n' \"$d\" \"$f\"",
                "done | sort -r | head -n \"$MAX\""
            });
        }

        private static string BashEscape(string value) =>
            "'" + value.Replace("'", "'\\''") + "'";

        private static string RunBash(string script)
        {
            try
            {
                using var process = new System.Diagnostics.Process();
                process.StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = "-lc " + BashEscape(script),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                process.Start();
                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit(15000);
                if (process.ExitCode != 0)
                {
                    Debug.LogWarning($"[MacSystemRecentProvider] mdfind query failed (exit {process.ExitCode}): {stderr.Trim()}");
                    return null;
                }

                return stdout;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MacSystemRecentProvider] {ex.Message}");
                return null;
            }
        }
    }
}
