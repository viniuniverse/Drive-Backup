using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace BackupDriver.Core.Config;

// v1.6: Interface de armazenamento seguro com suporte a migração automática
public interface ISecureStorage
{
    string Encrypt(string plainText);
    string Decrypt(string encryptedText);
    /// <summary>
    /// v1.6: Detecta se o valor já está criptografado (DPAPI + Base64).
    /// Retorna false se for texto puro — usado na migração automática.
    /// </summary>
    bool IsEncrypted(string value);
}

public class SecureStorage : ISecureStorage
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("BackupDriver_v1");

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            throw new PlatformNotSupportedException("DPAPI is only supported on Windows.");

        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var encryptedBytes = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.LocalMachine);
        return Convert.ToBase64String(encryptedBytes);
    }

    public string Decrypt(string encryptedText)
    {
        if (string.IsNullOrEmpty(encryptedText))
            return string.Empty;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            throw new PlatformNotSupportedException("DPAPI is only supported on Windows.");

        var encryptedBytes = Convert.FromBase64String(encryptedText);
        var plainBytes = ProtectedData.Unprotect(encryptedBytes, Entropy, DataProtectionScope.LocalMachine);
        return Encoding.UTF8.GetString(plainBytes);
    }

    // v1.6: Migração automática de texto puro → criptografado
    // Tenta decodificar Base64 e descriptografar via DPAPI.
    // Se qualquer etapa falhar, o valor é texto puro (legado).
    public bool IsEncrypted(string value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        try
        {
            var bytes = Convert.FromBase64String(value);
            ProtectedData.Unprotect(bytes, Entropy, DataProtectionScope.LocalMachine);
            return true; // Descriptografou com sucesso = já estava criptografado
        }
        catch
        {
            return false; // Falhou = texto puro (legado)
        }
    }
}
