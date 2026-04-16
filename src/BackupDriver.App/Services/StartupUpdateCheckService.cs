using System;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BackupDriver.App.Messages;
using CommunityToolkit.Mvvm.Messaging;

namespace BackupDriver.App.Services;

/// <summary>
/// Serviço de verificação de atualização executado na inicialização do app.
/// Consulta a API do GitHub Releases para comparar com a versão atual do assembly.
/// Opera de forma assíncrona (fire-and-forget) — não bloqueia a abertura da UI.
/// Em caso de falha de rede ou qualquer erro, opera silenciosamente.
/// </summary>
public class StartupUpdateCheckService
{
    private readonly HttpClient _httpClient;

    // URL da API do GitHub Releases — retorna a release mais recente do repositório
    private const string GitHubReleasesApiUrl =
        "https://api.github.com/repos/viniuniverse/Drive-Backup/releases/latest";

    public StartupUpdateCheckService(HttpClient httpClient)
    {
        _httpClient = httpClient;

        // Header obrigatório pela API do GitHub para identificar o cliente
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("BackupDriver-App");
    }

    /// <summary>
    /// Verifica se há nova versão disponível no GitHub Releases.
    /// Ao encontrar nova versão, dispara uma <see cref="UpdateAvailableMessage"/>
    /// via WeakReferenceMessenger para o MainWindowViewModel exibir o dialog.
    /// Timeout de 5 segundos para não atrasar a inicialização do app.
    /// </summary>
    public async Task CheckAsync()
    {
        try
        {
            // Timeout de 5s: app não pode esperar pela rede na inicialização
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            var response = await _httpClient.GetAsync(GitHubReleasesApiUrl, cts.Token);

            if (!response.IsSuccessStatusCode)
                return;

            var json = await response.Content.ReadAsStringAsync(cts.Token);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Extrai tag_name (ex: "v1.7.0") e html_url da release
            var tagName = root.GetProperty("tag_name").GetString();
            var releaseUrl = root.GetProperty("html_url").GetString();

            if (string.IsNullOrWhiteSpace(tagName) || string.IsNullOrWhiteSpace(releaseUrl))
                return;

            // Remove prefixo "v" ou "V" para comparação com System.Version
            var cleanTag = tagName.TrimStart('v', 'V');

            // Comparação semântica correta via System.Version
            // Evita bugs de comparação string: "1.6.10" seria < "1.6.9" em string
            if (!System.Version.TryParse(cleanTag, out var remoteVersion))
                return;

            // Versão atual lida do Assembly em runtime — sincronizada com o .csproj
            var localVersion = Assembly.GetExecutingAssembly().GetName().Version;

            if (localVersion != null && remoteVersion > localVersion)
            {
                // Nova versão encontrada: notifica o MainWindowViewModel via Messenger
                WeakReferenceMessenger.Default.Send(
                    new UpdateAvailableMessage(tagName, releaseUrl));
            }
        }
        catch
        {
            // Falha silenciosa — ausência de internet não deve afetar o app
            // O usuário não precisa saber que a verificação falhou
        }
    }
}
