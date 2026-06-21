using System.Collections.Generic;

namespace SCZip.Core
{
    public sealed class NavigationFrame
    {
        public string Path { get; set; }
        public string DisplayTitle { get; set; }
        public Domain.NavigationSource Source { get; set; }
        public bool IsArchiveInner { get; set; }
        public string ArchivePath { get; set; }
        public string ArchiveInnerPath { get; set; }
    }

    public sealed class NavigationStack
    {
        private readonly List<NavigationFrame> _frames = new();

        public IReadOnlyList<NavigationFrame> Frames => _frames;
        public NavigationFrame Current => _frames.Count > 0 ? _frames[_frames.Count - 1] : null;
        public int Depth => _frames.Count;

        public void Reset(NavigationFrame root)
        {
            _frames.Clear();
            _frames.Add(root);
        }

        public void Push(NavigationFrame frame) => _frames.Add(frame);

        public bool Pop()
        {
            if (_frames.Count <= 1) return false;
            _frames.RemoveAt(_frames.Count - 1);
            return true;
        }

        public string BuildBreadcrumb()
        {
            if (_frames.Count == 0) return "";
            var parts = new List<string>();
            foreach (var f in _frames)
                parts.Add(f.DisplayTitle);
            return string.Join(" › ", parts);
        }
    }
}
