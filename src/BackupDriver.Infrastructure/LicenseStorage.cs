using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace BackupDriver.Infrastructure;

/// <summary>
/// Armazenamento seguro da chave de licença usando DPAPI do Windows (ProtectedData).
///
/// SEGURANÇA (ADR-005 / Tarefa 2):
///   - A licença é criptografada com DPAPI (DataProtectionScope.CurrentUser).
///   - O arquivo resultante é legível SOMENTE pelo mesmo usuário Windows na mesma máquina.
///   - Mesmo abrindo o arquivo, o usuário vê apenas bytes cifrados — não pode editar manualmente.
///   - Isso impede a adulteração direta do arquivo de licença.
///
/// Localização do arquivo: %APPDATA%\BackupDriver\license.dat
/// </summary>
public class LicenseStorage : ILicenseStorage
{
    // Caminho do arquivo de licença na pasta de dados do usuário Windows
    private static readonly string LicenseFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BackupDriver",
        "license.dat");

    // Entropia adicional — aumenta a especificidade do DPAPI para este aplicativo
    // Mantê-la constante garante que o mesmo app sempre consiga decriptografar
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("BackupDriver-PRO-2026-Vg");

    /// <summary>
    /// Criptografa e salva a chave de licença com DPAPI.
    /// </summary>
    public void Save(string licenseKey)
    {
        var dir = Path.GetDirectoryName(LicenseFilePath)!;
        Directory.CreateDirectory(dir);

        var plainBytes      = Encoding.UTF8.GetBytes(licenseKey);
        var encryptedBytes  = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(LicenseFilePath, encryptedBytes);
    }

    /// <summary>
    /// Carrega e decriptografa a chave de licença. Retorna null se não existir ou falhar.
    /// </summary>
    public string? Load()
    {
        if (!File.Exists(LicenseFilePath))
            return null;

        try
        {
            var encryptedBytes = File.ReadAllBytes(LicenseFilePath);
            var plainBytes     = ProtectedData.Unprotect(encryptedBytes, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            // Arquivo corrompido ou de outro usuário/máquina — tratar como sem licença
            return null;
        }
    }

    /// <summary>
    /// Remove o arquivo de licença do disco.
    /// </summary>
    public void Clear()
    {
        if (File.Exists(LicenseFilePath))
            File.Delete(LicenseFilePath);
    }
}
