using System.Collections.Generic;
using System.Threading.Tasks;

namespace SCZip.Services
{
    public interface IFileSystemService
    {
        string MyFilesRoot { get; }
        string StorageRoot { get; }
        string PhotosRoot { get; }
        string MusicRoot { get; }

        Task<IReadOnlyList<Domain.FileEntry>> ListDirectoryAsync(string path, Domain.NavigationSource source);
        IReadOnlyList<Domain.FileEntry> ListDirectory(string path, Domain.NavigationSource source);
        Task CopyAsync(string src, string dest);
        Task MoveAsync(string src, string dest);
        Task DeleteAsync(string path);
        Task CreateDirectoryAsync(string path);
        Task RenameAsync(string src, string dest);
        void EnsureAppDirectories();
        bool Exists(string path);
        string GetParent(string path);
        string Combine(string a, string b);
    }
}
