namespace BackupDriver.Infrastructure;

/// <summary>
/// Contrato para persistência segura da chave de licença em disco.
/// A implementação usa DPAPI Windows (ProtectedData) para criptografar o arquivo.
/// </summary>
public interface ILicenseStorage
{
    /// <summary>Salva a chave de licença de forma segura em disco.</summary>
    void Save(string licenseKey);

    /// <summary>Carrega a chave de licença salva. Retorna null se não houver licença.</summary>
    string? Load();

    /// <summary>Remove a licença salva (usado em caso de reset ou chave inválida detectada).</summary>
    void Clear();
}
