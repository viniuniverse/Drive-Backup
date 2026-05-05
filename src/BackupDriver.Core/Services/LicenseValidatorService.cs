using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BackupDriver.Core.Services;

/// <summary>
/// Implementação do validador de licença usando criptografia assimétrica RSA-2048.
///
/// SEGURANÇA (ADR-005):
///   - A chave PÚBLICA está embutida no código (somente verificação).
///   - A chave PRIVADA fica exclusivamente com o time de engenharia para geração de licenças.
///   - Formato da licença: {claims_json_base64}|{assinatura_rsa_base64}
///
/// OFUSCAÇÃO: Este serviço é processado pelo Obfuscar na build Release.
/// Métodos críticos NÃO expõem a chave ou a lógica de verificação de forma legível.
/// </summary>
public class LicenseValidatorService : ILicenseValidatorService
{
    // ─── Chave pública RSA-2048 embutida no binário ───────────────────────────
    // Gerada em 2026-05-05. Corresponde ao par em C:\DRIVE BACKUP\keys\private.xml
    // NÃO alterar sem regenerar todas as licenças existentes.
    private const string PublicKeyXml =
        "<RSAKeyValue>" +
        "<Modulus>1k6b5cce57SaiOdWK9gzostwdcAX0F42Wp9zatwQwWN52hCm9jRgNDdhfdjYDzZ4jzIvzsDs76XtpetcvGhUIphzp3dHEiohTiBV47Njlfz3ReQiKzPXIyHXym29tB6Ro/5CB6wFZntDrwJj6kmUMfC4gSLZhXL2CSPqkYc9oVGneOFvDTEXg2cI49VNU4HoZWh+zf/UgPSrHhSVIDK+f20Lj+wFl6076ElRtcqmWxwfIJoMAi8kLAEzVryULRyaZ6hlGpIg+WpvEvXpdFM+8k1oMYEE2yHM4aKTxA0lIoftd3Y42N6eaKSAxZom+HxuuivFiEp8qlzHuAZOl9PeuQ==</Modulus>" +
        "<Exponent>AQAB</Exponent>" +
        "</RSAKeyValue>";

    /// <summary>
    /// Valida uma chave de licença verificando sua assinatura RSA e extraindo os claims.
    /// </summary>
    /// <param name="licenseKey">Formato esperado: {claims_base64}|{assinatura_base64}</param>
    /// <returns>LicenseClaims se a assinatura for válida e a licença não estiver expirada; null caso contrário.</returns>
    public LicenseClaims? ValidateLicense(string licenseKey)
    {
        // Proteção contra entradas nulas ou malformadas
        if (string.IsNullOrWhiteSpace(licenseKey))
            return null;

        try
        {
            // ── Separar claims da assinatura ──────────────────────────────────
            var parts = licenseKey.Trim().Split('|');
            if (parts.Length != 2)
                return null;

            var claimsBase64    = parts[0];
            var signatureBase64 = parts[1];

            // ── Decodificar componentes ───────────────────────────────────────
            byte[] claimsBytes;
            byte[] signatureBytes;

            try
            {
                claimsBytes    = Convert.FromBase64String(claimsBase64);
                signatureBytes = Convert.FromBase64String(signatureBase64);
            }
            catch (FormatException)
            {
                // Base64 inválido — chave adulterada
                return null;
            }

            // ── Verificar assinatura RSA-SHA256 com a chave pública ───────────
            using var rsa = RSA.Create();
            rsa.FromXmlString(PublicKeyXml);

            bool isSignatureValid = rsa.VerifyData(
                claimsBytes,
                signatureBytes,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            if (!isSignatureValid)
                return null;

            // ── Deserializar os claims JSON ────────────────────────────────────
            var claimsJson = Encoding.UTF8.GetString(claimsBytes);
            var claims = JsonSerializer.Deserialize<LicenseClaimsDto>(claimsJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (claims == null)
                return null;

            // ── Construir e retornar claims tipados ───────────────────────────
            var result = new LicenseClaims
            {
                Tier    = claims.Tier   ?? string.Empty,
                Client  = claims.Client ?? string.Empty,
                Expires = claims.Expires.HasValue
                    ? DateTime.SpecifyKind(claims.Expires.Value, DateTimeKind.Utc)
                    : null
            };

            // Verificar expiração
            return result.IsValid ? result : null;
        }
        catch
        {
            // Qualquer exceção inesperada → licença inválida (fail-safe)
            return null;
        }
    }

    // ─── DTO interno para deserialização ─────────────────────────────────────
    // Classe privada para isolar a estrutura JSON dos claims externos
    private sealed class LicenseClaimsDto
    {
        public string? Tier    { get; set; }
        public string? Client  { get; set; }
        public DateTime? Expires { get; set; }
    }
}
