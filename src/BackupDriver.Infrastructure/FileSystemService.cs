namespace BackupDriver.Infrastructure;

public interface IFileSystemService
{
    bool FileExists(string path);
    bool DirectoryExists(string path);
    void EnsureDirectoryExists(string path);
    void DeleteFile(string path);
    IEnumerable<string> GetFiles(string directory, string pattern, bool recursive = false);
    void CopyFile(string source, string destination, bool overwrite = false);
    long GetFileSize(string path);
    DateTime GetLastWriteTime(string path);
    void DeleteEmptyDirectories(string path);
}

public class FileSystemService : IFileSystemService
{
    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public void EnsureDirectoryExists(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }

    public void DeleteFile(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    public IEnumerable<string> GetFiles(string directory, string pattern, bool recursive = false)
    {
        if (!Directory.Exists(directory))
            return Enumerable.Empty<string>();
        
        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        return Directory.GetFiles(directory, pattern, searchOption);
    }

    public void CopyFile(string source, string destination, bool overwrite = false)
    {
        File.Copy(source, destination, overwrite);
    }

    public long GetFileSize(string path) => new FileInfo(path).Length;

    public DateTime GetLastWriteTime(string path) => File.GetLastWriteTime(path);

    // v1.6.4: Organização de pastas - limpar diretórios vazios recursivamente
    public void DeleteEmptyDirectories(string path)
    {
        if (!Directory.Exists(path))
            return;

        foreach (var dir in Directory.GetDirectories(path))
        {
            DeleteEmptyDirectories(dir);
        }

        if (Directory.GetDirectories(path).Length == 0 && Directory.GetFiles(path).Length == 0)
        {
            try
            {
                Directory.Delete(path, false);
            }
            catch
            {
                // Ignorar falhas (ex: em uso)
            }
        }
    }
}
