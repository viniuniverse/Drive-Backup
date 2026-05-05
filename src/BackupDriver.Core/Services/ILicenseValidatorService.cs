using System;

namespace BackupDriver.Core.Services;

/// <summary>
/// Representa as informações (claims) contidas em uma licença PRO válida.
/// </summary>
public class LicenseClaims
{
    /// <summary>Tier da licença: "PRO" ou futuras variações.</summary>
    public string Tier { get; init; } = string.Empty;

    /// <summary>Data de expiração da licença (UTC). Null = sem expiração.</summary>
    public DateTime? Expires { get; init; }

    /// <summary>Nome do cliente / empresa licenciada.</summary>
    public string Client { get; init; } = string.Empty;

    /// <summary>Retorna true se a licença ainda é válida considerando a data atual.</summary>
    public bool IsValid => Expires == null || Expires.Value > DateTime.UtcNow;
}

/// <summary>
/// Contrato do validador de licença.
/// Implementações devem usar criptografia assimétrica (RSA) — nunca hash simples.
/// </summary>
public interface ILicenseValidatorService
{
    /// <summary>
    /// Valida uma chave de licença e extrai seus claims.
    /// </summary>
    /// <param name="licenseKey">String da licença no formato: {claims_json_base64}|{assinatura_base64}</param>
    /// <returns>LicenseClaims se válida e não expirada; null caso contrário.</returns>
    LicenseClaims? ValidateLicense(string licenseKey);
}
