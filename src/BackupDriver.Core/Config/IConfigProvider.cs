using BackupDriver.Infrastructure;

namespace BackupDriver.Core.Config;

public class EncryptedConfigProvider : IConfigProvider
{
    private readonly ISecureStorage _secureStorage;
    private readonly JsonConfigProvider _jsonProvider = new();

    private static readonly string[] EncryptedFields = ["Password", "ZipPassword", "BotToken"];

    public EncryptedConfigProvider(ISecureStorage secureStorage)
    {
        _secureStorage = secureStorage;
    }

    public T? Load<T>(string filePath) where T : class
    {
        return _jsonProvider.Load<T>(filePath);
    }

    public void Save<T>(string filePath, T config) where T : class
    {
        _jsonProvider.Save(filePath, config);
    }

    public bool Exists(string filePath) => _jsonProvider.Exists(filePath);
}
