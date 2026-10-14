using System.Globalization;
using System.Text.Json;
using Atlas.Application.Business.Common;
using Atlas.Application.Business.Conteudo.Common;
using Atlas.Domain.Business.Conteudo.Entities;
using Microsoft.Extensions.Logging;

namespace Atlas.Application.Business.Conteudo.Cotacoes;

/// <summary>
/// Atualiza a cotação do dólar e do euro pela PTAX do Banco Central (dados
/// abertos, sem chave). Lê os boletins dos últimos dias: o mais recente é a
/// cotação atual e o último fechamento de um dia anterior dá a variação.
/// </summary>
/// <remarks>
/// O Banco Central publica boletins só em dias úteis. No fim de semana fica
/// valendo o último (a validade na TV é de 4 dias).
/// </remarks>
public sealed class ColetorCotacoes(
    IApplicationDbContext context,
    IClienteConteudoExterno cliente,
    IConfiguracaoIndoor configuracao,
    TimeProvider timeProvider,
    ILogger<ColetorCotacoes> logger)
{
    private const long TamanhoMaximoRespostaBytes = 512 * 1024;

    /// <summary>Janela de busca: cobre feriados prolongados.</summary>
    private const int DiasConsultados = 10;

    /// <summary>Os boletins são publicados no horário de Brasília (sem horário de verão desde 2019).</summary>
    private static readonly TimeSpan FusoBrasilia = TimeSpan.FromHours(-3);

    public async Task AtualizarTodasAsync(CancellationToken cancellationToken)
    {
        foreach (var (codigo, _) in Cotacao.Moedas)
        {
            await AtualizarAsync(codigo, cancellationToken);
        }
    }

    public async Task AtualizarAsync(string moeda, CancellationToken cancellationToken)
    {
        var agora = timeProvider.GetUtcNow();
        var cotacao = await context.Cotacoes.FirstOrDefaultAsync(c => c.Moeda == moeda, cancellationToken);

        try
        {
            var boletins = await LerBoletinsAsync(moeda, agora, cancellationToken);
            if (boletins.Count == 0)
            {
                throw new InvalidOperationException($"O Banco Central não publicou cotação de {moeda} nos últimos {DiasConsultados} dias.");
            }

            var atual = boletins.MaxBy(b => b.CotadaEm)!;
            var fechamentoAnterior = boletins
                .Where(b => b.Fechamento && b.CotadaEm.Date < atual.CotadaEm.Date)
                .MaxBy(b => b.CotadaEm);

            if (cotacao is null)
            {
                cotacao = new Cotacao { Moeda = moeda };
                context.Cotacoes.Add(cotacao);
            }

            cotacao.Compra = atual.Compra;
            cotacao.Venda = atual.Venda;
            cotacao.Boletim = atual.Tipo;
            // O PostgreSQL (timestamptz) só grava em UTC; a comparação de dias acima é em Brasília.
            cotacao.CotadaEm = atual.CotadaEm.ToUniversalTime();
            cotacao.FechamentoAnterior = fechamentoAnterior?.Venda;
            cotacao.AtualizadaEm = agora;
            cotacao.UltimoErro = null;
        }
        catch (Exception ex) when (ex is ConteudoExternoIndisponivelException or JsonException or InvalidOperationException or KeyNotFoundException)
        {
            logger.LogWarning("Cotação de {Moeda} não atualizada: {Erro}", moeda, ex.Message);
            if (cotacao is not null)
            {
                cotacao.UltimoErro = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<Boletim>> LerBoletinsAsync(string moeda, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        var hoje = agora.ToOffset(FusoBrasilia).Date;
        var inicio = hoje.AddDays(-DiasConsultados);
        static string Data(DateTime d) => d.ToString("MM-dd-yyyy", CultureInfo.InvariantCulture);

        var url = new Uri(
            $"{configuracao.UrlPtax.AbsoluteUri.TrimEnd('/')}/CotacaoMoedaPeriodo(moeda=@moeda,dataInicial=@dataInicial,dataFinalCotacao=@dataFinalCotacao)"
            + $"?%40moeda='{moeda}'&%40dataInicial='{Data(inicio)}'&%40dataFinalCotacao='{Data(hoje)}'"
            + "&%24format=json&%24select=cotacaoCompra,cotacaoVenda,dataHoraCotacao,tipoBoletim");

        var resposta = await cliente.BaixarAsync(url, TamanhoMaximoRespostaBytes, cancellationToken);
        using var json = JsonDocument.Parse(resposta.Bytes);

        var boletins = new List<Boletim>();
        foreach (var item in json.RootElement.GetProperty("value").EnumerateArray())
        {
            var texto = item.GetProperty("dataHoraCotacao").GetString();
            if (!DateTime.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dataHora))
            {
                continue;
            }

            var tipo = item.TryGetProperty("tipoBoletim", out var t) ? t.GetString() : null;
            boletins.Add(new Boletim(
                item.GetProperty("cotacaoCompra").GetDecimal(),
                item.GetProperty("cotacaoVenda").GetDecimal(),
                new DateTimeOffset(dataHora, FusoBrasilia),
                tipo,
                tipo?.Contains("Fechamento", StringComparison.OrdinalIgnoreCase) == true));
        }

        return boletins;
    }

    private sealed record Boletim(decimal Compra, decimal Venda, DateTimeOffset CotadaEm, string? Tipo, bool Fechamento);
}
