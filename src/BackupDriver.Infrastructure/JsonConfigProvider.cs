using System.Text.Json;

namespace BackupDriver.Infrastructure;

public interface IConfigProvider
{
    T? Load<T>(string filePath) where T : class;
    void Save<T>(string filePath, T config) where T : class;
    bool Exists(string filePath);
}

public class JsonConfigProvider : IConfigProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public T? Load<T>(string filePath) where T : class
    {
        if (!File.Exists(filePath))
            return null;

        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    public void Save<T>(string filePath, T config) where T : class
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        if (File.Exists(filePath))
        {
            var attributes = File.GetAttributes(filePath);
            File.SetAttributes(filePath, attributes & ~FileAttributes.Hidden & ~FileAttributes.ReadOnly);
        }

        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(filePath, json);
    }

    public bool Exists(string filePath) => File.Exists(filePath);
}
