using Atlas.Application.Business.Common;
using Microsoft.Extensions.Configuration;

namespace Atlas.Infrastructure.Business;

/// <summary>Lê a seção <c>Indoor</c> do appsettings.</summary>
internal sealed class ConfiguracaoIndoor(IConfiguration configuration) : IConfiguracaoIndoor
{
    public string UrlPainel =>
        configuration["Indoor:UrlPainel"]
        ?? throw new InvalidOperationException("Configuração 'Indoor:UrlPainel' ausente no appsettings.");

    public TimeSpan IntervaloSincronizacao =>
        TimeSpan.FromSeconds(Inteiro("Indoor:Dispositivo:IntervaloSincronizacaoSegundos", padrao: 300, minimo: 30));

    public TimeSpan ValidadeTokenDispositivo =>
        TimeSpan.FromDays(Inteiro("Indoor:Dispositivo:ValidadeTokenDias", padrao: 30, minimo: 1));

    public TimeSpan ValidadeUrlDownload =>
        TimeSpan.FromHours(Inteiro("Indoor:Dispositivo:ValidadeUrlDownloadHoras", padrao: 24, minimo: 1));

    private int Inteiro(string chave, int padrao, int minimo) =>
        int.TryParse(configuration[chave], out var valor) ? Math.Max(valor, minimo) : padrao;
}
