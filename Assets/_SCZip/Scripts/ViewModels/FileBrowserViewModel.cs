using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SCZip.Core;
using SCZip.Domain;
using SCZip.Infrastructure;
using SCZip.Services;

namespace SCZip.ViewModels
{
    public sealed class FileBrowserViewModel
    {
        private readonly NavigationStack _stack = new();
        private readonly HashSet<string> _selected = new();
        private List<FileEntry> _items = new();
        private List<ArchiveEntry> _archiveItems = new();
        private CancellationTokenSource _loadCts;
        private int _loadGeneration;

        public event Action StateChanged;

        public IReadOnlyList<FileEntry> Items => _items;
        public IReadOnlyList<ArchiveEntry> ArchiveItems => _archiveItems;
        public IReadOnlyCollection<string> SelectedIds => _selected;
        public int SelectedCount => _selected.Count;
        public bool IsArchiveInner => _stack.Current?.IsArchiveInner == true;
        public string Title => _stack.Current?.DisplayTitle ?? "SCZip";
        public string Subtitle { get; private set; }
        public string Breadcrumb => _stack.BuildBreadcrumb();
        public bool IsLoading { get; private set; }
        public string ErrorMessage { get; private set; }
        public NavigationSource CurrentSource => _stack.Current?.Source ?? NavigationSource.Storage;
        public IReadOnlyList<FileAction> AvailableActions =>
            ActionBarResolver.Resolve(IsArchiveInner, GetSelectedEntries());

        public SelectAllState SelectAllState
        {
            get
            {
                var total = IsArchiveInner ? _archiveItems.Count : _items.Count;
                if (total == 0 || _selected.Count == 0) return SelectAllState.None;
                return _selected.Count >= total ? SelectAllState.All : SelectAllState.Some;
            }
        }

        public void NavigateToStartupFolder()
        {
            AppServices.EnsureInitialized();
            var dir = AppServices.FileSystem.ExecutableDirectory;
            var title = Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrEmpty(title))
                title = "Files";

            NavigateToFolder(dir, title, ResolveSourceForPath(dir));
        }

        public void NavigateToSource(NavigationSource source)
        {
            AppServices.EnsureInitialized();
            var fs = AppServices.FileSystem;
            var path = source switch
            {
                NavigationSource.MyFiles => fs.MyFilesRoot,
                NavigationSource.Storage => fs.StorageRoot,
                NavigationSource.Photos => fs.PhotosRoot,
                NavigationSource.Music => fs.MusicRoot,
                _ => fs.StorageRoot
            };

            var title = source switch
            {
                NavigationSource.Recent => "Recent",
                NavigationSource.MyFiles => "My Files",
                NavigationSource.Storage => "Storage",
                NavigationSource.Photos => "Photos",
                NavigationSource.Music => "Music",
                _ => "Files"
            };

            NavigateToFolder(path, title, source);
        }

        public void NavigateToFolder(string path, string displayTitle, NavigationSource source)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                ErrorMessage = "Folder not found: " + path;
                Notify();
                return;
            }

            _stack.Reset(new NavigationFrame
            {
                Path = path,
                DisplayTitle = displayTitle,
                Source = source,
                IsArchiveInner = false
            });
            ClearSelection();
            _ = LoadCurrentAsync();
        }

        public string CurrentFolderPath =>
            _stack.Current is { IsArchiveInner: false } frame ? frame.Path : null;

        public bool CanGoBack => _stack.Depth > 1;

        /// <summary>Loads Recent on a background thread — do not call GetRecent() synchronously from UI code.</summary>
        public void ShowRecent() => _ = ShowRecentAsync();

        private async Task ShowRecentAsync()
        {
            AppServices.EnsureInitialized();

            _loadCts?.Cancel();
            _loadCts = new CancellationTokenSource();
            var ct = _loadCts.Token;
            var generation = ++_loadGeneration;

            IsLoading = true;
            ErrorMessage = null;
            Notify();

            try
            {
                await LoadRecentItemsAsync(generation, ct);
            }
            finally
            {
                if (generation == _loadGeneration)
                {
                    IsLoading = false;
                    Notify();
                }
            }
        }

        public bool GoBack() => GoBackAndReloadSync();

        /// <summary>Pop stack and reload listing on the main thread (used by back navigation).</summary>
        public bool GoBackAndReloadSync()
        {
            if (!_stack.Pop())
                return false;

            ClearSelection();
            IsLoading = false;
            ErrorMessage = null;

            var frame = _stack.Current;
            if (frame == null)
            {
                Notify();
                return true;
            }

            if (frame.Source == NavigationSource.Recent)
            {
                ShowRecent();
                return true;
            }

            if (frame.IsArchiveInner)
            {
                _ = LoadCurrentAsync();
                return true;
            }

            try
            {
                _items = AppServices.FileSystem.ListDirectory(frame.Path, frame.Source).ToList();
                _archiveItems.Clear();
                Subtitle = frame.Path;
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }

            Notify();
            return true;
        }

        public void OpenEntry(FileEntry entry)
        {
            if (entry == null) return;

            if (entry.IsDirectory)
            {
                _stack.Push(new NavigationFrame
                {
                    Path = entry.FullPath,
                    DisplayTitle = entry.Name,
                    Source = entry.Source
                });
                ClearSelection();
                _ = LoadCurrentAsync();
                return;
            }

            if (entry.IsArchive)
            {
                AppServices.Recent.AddRecent(entry.FullPath, entry.ArchiveFormat);
                _stack.Push(new NavigationFrame
                {
                    Path = entry.FullPath,
                    DisplayTitle = entry.Name,
                    Source = entry.Source,
                    IsArchiveInner = true,
                    ArchivePath = entry.FullPath,
                    ArchiveInnerPath = ""
                });
                ClearSelection();
                _ = LoadCurrentAsync();
                return;
            }

            var parent = Path.GetDirectoryName(entry.FullPath);
            if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent))
                NavigateToFolder(parent, Path.GetFileName(parent), ResolveSourceForPath(parent));
        }

        private static NavigationSource ResolveSourceForPath(string path)
        {
            var fs = AppServices.FileSystem;
            if (path.StartsWith(fs.MyFilesRoot)) return NavigationSource.MyFiles;
            if (path.StartsWith(fs.StorageRoot)) return NavigationSource.Storage;
            if (path.StartsWith(fs.PhotosRoot)) return NavigationSource.Photos;
            if (path.StartsWith(fs.MusicRoot)) return NavigationSource.Music;
            return NavigationSource.MyFiles;
        }

        public void ToggleSelect(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (_selected.Contains(id)) _selected.Remove(id);
            else _selected.Add(id);
            Notify();
        }

        public void ToggleSelectAll()
        {
            if (SelectAllState == SelectAllState.All)
            {
                ClearSelection();
                return;
            }

            if (IsArchiveInner)
            {
                foreach (var e in _archiveItems)
                    _selected.Add(e.Path);
            }
            else
            {
                foreach (var e in _items)
                    _selected.Add(e.Id);
            }

            Notify();
        }

        public void ClearSelection()
        {
            _selected.Clear();
            Notify();
        }

        public async Task LoadCurrentAsync()
        {
            _loadCts?.Cancel();
            _loadCts = new CancellationTokenSource();
            var ct = _loadCts.Token;
            var generation = ++_loadGeneration;

            IsLoading = true;
            ErrorMessage = null;
            Notify();

            try
            {
                var frame = _stack.Current;
                if (frame == null)
                    return;

                if (frame.Source == NavigationSource.Recent)
                {
                    await LoadRecentItemsAsync(generation, ct);
                    return;
                }

                if (frame.IsArchiveInner)
                {
                    var entries = await AppServices.Archive.ListEntriesAsync(frame.ArchivePath, new ArchiveOpenOptions(), ct);
                    if (generation != _loadGeneration || ct.IsCancellationRequested)
                        return;

                    _archiveItems = entries.ToList();
                    _items.Clear();
                    var info = new FileInfo(frame.ArchivePath);
                    var fmt = ArchiveFormatRegistry.DetectByPath(frame.ArchivePath);
                    var fmtLabel = ArchiveFormatRegistry.GetDisplayLabel(fmt).Split(' ')[0];
                    Subtitle = $"{_archiveItems.Count} files · {FileEntry.FormatSize(info.Length)} · {fmtLabel}";
                }
                else
                {
                    // Local folders are small — load synchronously to avoid startup race / stuck loading UI.
                    _items = AppServices.FileSystem.ListDirectory(frame.Path, frame.Source).ToList();
                    if (generation != _loadGeneration || ct.IsCancellationRequested)
                        return;

                    _archiveItems.Clear();
                    Subtitle = frame.Path;
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                if (generation == _loadGeneration)
                    ErrorMessage = ex.Message;
            }
            finally
            {
                if (generation == _loadGeneration)
                {
                    IsLoading = false;
                    Notify();
                }
            }
        }

        private async Task LoadRecentItemsAsync(int generation, CancellationToken ct)
        {
            try
            {
                var items = await Task.Run(() => AppServices.Recent.GetRecent().ToList(), ct);
                if (generation != _loadGeneration || ct.IsCancellationRequested)
                    return;

                _items = items;
                _archiveItems.Clear();
                _stack.Reset(new NavigationFrame
                {
                    Path = "",
                    DisplayTitle = "Recent",
                    Source = NavigationSource.Recent
                });
                Subtitle = $"{_items.Count} items";
                ClearSelection();
            }
            catch (OperationCanceledException)
            {
                // superseded by a newer navigation
            }
            catch (Exception ex)
            {
                if (generation == _loadGeneration)
                    ErrorMessage = ex.Message;
            }
        }

        /// <summary>PlayMode tests — load directory listing on the main thread.</summary>
        public void ReloadDirectorySyncForTests()
        {
            var frame = _stack.Current;
            if (frame == null || frame.IsArchiveInner || frame.Source == NavigationSource.Recent)
                return;

            IsLoading = false;
            ErrorMessage = null;

            try
            {
                _items = AppServices.FileSystem.ListDirectory(frame.Path, frame.Source).ToList();
                _archiveItems.Clear();
                Subtitle = frame.Path;
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        public async Task DeleteSelectedAsync()
        {
            foreach (var entry in GetSelectedEntries())
                await AppServices.FileSystem.DeleteAsync(entry.FullPath);
            ClearSelection();
            await LoadCurrentAsync();
        }

        public async Task CreateArchiveAsync(string outputPath, ArchiveFormat format, string password = null)
        {
            var sources = GetSelectedEntries().Select(e => e.FullPath).ToList();
            if (sources.Count == 0) return;

            var options = new CreateArchiveOptions
            {
                Format = format,
                OutputPath = outputPath,
                SourcePaths = sources,
                Level = AppServices.Settings.CompressionLevel,
                Password = password
            };

            await AppServices.Archive.CreateAsync(options, null, CancellationToken.None);
            ClearSelection();
            await LoadCurrentAsync();
        }

        public async Task ExtractSelectedAsync(string destDir)
        {
            var frame = _stack.Current;
            if (frame == null) return;

            if (frame.IsArchiveInner)
            {
                var paths = _selected.ToList();
                await AppServices.Archive.ExtractAsync(new ExtractOptions
                {
                    ArchivePath = frame.ArchivePath,
                    DestinationDirectory = destDir,
                    EntryPaths = paths
                }, null, CancellationToken.None);
            }
            else
            {
                var archive = GetSelectedEntries().FirstOrDefault(e => e.IsArchive);
                if (archive == null) return;
                await AppServices.Archive.ExtractAsync(new ExtractOptions
                {
                    ArchivePath = archive.FullPath,
                    DestinationDirectory = destDir
                }, null, CancellationToken.None);
            }

            ClearSelection();
            await LoadCurrentAsync();
        }

        public async Task CreateFolderAsync(string name)
        {
            var frame = _stack.Current;
            if (frame == null || frame.IsArchiveInner) return;
            var path = AppServices.FileSystem.Combine(frame.Path, name);
            await AppServices.FileSystem.CreateDirectoryAsync(path);
            await LoadCurrentAsync();
        }

        public List<FileEntry> GetSelectedEntries() =>
            _items.Where(i => _selected.Contains(i.Id)).ToList();

        private void Notify() => StateChanged?.Invoke();
    }

    public enum SelectAllState
    {
        None,
        Some,
        All
    }
}
