using System.Text.Json;
using Atlas.Domain.Platform.Entities;
using Atlas.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Business.Parametros;

/// <summary>
/// Na subida, cadastra os parâmetros da Mídia Indoor que ainda não existem
/// (com o valor que estava no appsettings, ou o padrão) e, a cada minuto,
/// relê a tabela: o que a operadora muda em Atlas Studio → Parâmetros vale em
/// até 1 min, sem reiniciar a API.
/// </summary>
internal sealed class SincronizadorParametrosIndoor(
    IServiceScopeFactory scopes,
    ParametrosIndoor parametros,
    IConfiguration configuration,
    TimeProvider timeProvider,
    ILogger<SincronizadorParametrosIndoor> logger) : BackgroundService
{
    public static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Cadastra e lê ANTES dos outros serviços em segundo plano (coletas,
    /// alertas, inventário), que já nascem com os valores do banco. No máximo
    /// 15 s: banco fora não segura a subida.
    /// </summary>
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limite.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            await ExecutarComLogAsync(CadastrarFaltantesAsync, "cadastrar", limite.Token);
            await ExecutarComLogAsync(LerAsync, "ler", limite.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("O banco demorou para entregar os parâmetros da Mídia Indoor; tento de novo em 1 min.");
        }

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(Intervalo, timeProvider, stoppingToken);
                await ExecutarComLogAsync(LerAsync, "ler", stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Desligando.
        }
    }

    /// <summary>
    /// Cria só o que falta; nunca sobrescreve o que a operadora mudou. Grava em
    /// nome do <c>admin</c>: a tela de Parâmetros da plataforma só lista linhas
    /// com autor (junta com o usuário criador), e aqui não há usuário logado.
    /// </summary>
    internal async Task CadastrarFaltantesAsync(CancellationToken cancellationToken)
    {
        await using var escopo = scopes.CreateAsyncScope();
        var context = escopo.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var adminId = await context.Users
            .Where(u => u.NormalizedUserName == "ADMIN")
            .Select(u => (long?)u.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var chaves = ParametrosIndoor.Todos.Select(p => p.Chave).ToList();
        var existentes = await context.SystemParameters
            .Where(p => chaves.Contains(p.Key))
            .ToListAsync(cancellationToken);

        // Cadastrados antes sem autor: some da tela de Parâmetros. Corrige.
        foreach (var semAutor in existentes.Where(p => p.CreatedBy is null || p.LastModifiedBy is null))
        {
            semAutor.CreatedBy ??= adminId;
            semAutor.LastModifiedBy ??= adminId;
        }

        var novos = ParametrosIndoor.Todos
            .Where(p => existentes.All(e => e.Key != p.Chave))
            .Select(p => new SystemParameter
            {
                Key = p.Chave,
                Value = ValorInicial(p),
                Description = p.Descricao,
                Group = ParametrosIndoor.Grupo,
                DataType = p.Tipo,
                CreatedBy = adminId,
                LastModifiedBy = adminId,
            })
            .ToList();

        context.SystemParameters.AddRange(novos);
        if (!context.ChangeTracker.HasChanges())
        {
            return;
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            if (novos.Count > 0)
            {
                logger.LogInformation("Parâmetros da Mídia Indoor cadastrados: {Chaves}.", string.Join(", ", novos.Select(p => p.Key)));
            }
        }
        catch (DbUpdateException)
        {
            // Outra instância cadastrou ao mesmo tempo (chave única): tudo bem.
            logger.LogDebug("Parâmetros da Mídia Indoor já cadastrados por outra instância.");
        }
    }

    internal async Task LerAsync(CancellationToken cancellationToken)
    {
        await using var escopo = scopes.CreateAsyncScope();
        var context = escopo.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var chaves = ParametrosIndoor.Todos.Select(p => p.Chave).ToList();
        var valores = await context.SystemParameters
            .AsNoTracking()
            .Where(p => chaves.Contains(p.Key))
            .Select(p => new KeyValuePair<string, string>(p.Key, p.Value))
            .ToListAsync(cancellationToken);

        parametros.Atualizar(valores, timeProvider.GetUtcNow());
    }

    /// <summary>O que estava no appsettings/variável de ambiente migra para o banco na primeira subida.</summary>
    private string ValorInicial(DefinicaoParametro definicao)
    {
        if (definicao.Tipo == "JSON")
        {
            var lista = configuration.GetSection(definicao.ChaveConfiguracao).Get<string[]>();
            return lista is { Length: > 0 } ? JsonSerializer.Serialize(lista) : definicao.Padrao;
        }

        var valor = configuration[definicao.ChaveConfiguracao];
        return string.IsNullOrWhiteSpace(valor) ? definicao.Padrao : valor.Trim();
    }

    private async Task ExecutarComLogAsync(Func<CancellationToken, Task> acao, string nome, CancellationToken cancellationToken)
    {
        try
        {
            await acao(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Não foi possível {Acao} os parâmetros da Mídia Indoor; seguem os valores da configuração.", nome);
        }
    }
}
