using System.Collections.Generic;
using System.Linq;
using SCZip.Domain;

namespace SCZip.ViewModels
{
    public static class ActionBarResolver
    {
        public static IReadOnlyList<FileAction> Resolve(bool isArchiveInner, IReadOnlyList<FileEntry> selected)
        {
            var count = selected?.Count ?? 0;
            if (count == 0) return new List<FileAction>();

            var archives = selected.Where(s => s.IsArchive).ToList();
            var nonArchives = selected.Where(s => !s.IsArchive).ToList();

            var actions = new List<FileAction>();

            if (isArchiveInner)
            {
                actions.Add(FileAction.Unzip);
                actions.Add(FileAction.Compress);
                actions.Add(FileAction.Share);
                actions.Add(FileAction.Delete);
                actions.Add(FileAction.More);
                return actions;
            }

            if (count == 1 && archives.Count == 1)
            {
                actions.Add(FileAction.Unzip);
                actions.Add(FileAction.Share);
                actions.Add(FileAction.Delete);
                actions.Add(FileAction.More);
                return actions;
            }

            actions.Add(FileAction.Compress);
            actions.Add(FileAction.Share);
            actions.Add(FileAction.Delete);
            actions.Add(FileAction.More);
            return actions;
        }

        public static bool CanRename(IReadOnlyList<FileEntry> selected) => selected?.Count == 1;
        public static bool CanUnzip(IReadOnlyList<FileAction> actions) => actions.Contains(FileAction.Unzip);
    }
}
