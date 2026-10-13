using Atlas.Application.Business.Common;
using Microsoft.Extensions.Configuration;

namespace Atlas.Infrastructure.Business;

/// <summary>Lê a seção <c>Indoor</c> do appsettings.</summary>
internal sealed class ConfiguracaoIndoor(IConfiguration configuration) : IConfiguracaoIndoor
{
    public string UrlPainel =>
        configuration["Indoor:UrlPainel"]
        ?? throw new InvalidOperationException("Configuração 'Indoor:UrlPainel' ausente no appsettings.");
}
