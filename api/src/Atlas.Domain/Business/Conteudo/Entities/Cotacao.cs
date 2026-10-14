namespace Atlas.Domain.Business.Conteudo.Entities;

/// <summary>
/// Cotação oficial de uma moeda (PTAX do Banco Central, dados abertos), lida
/// pela API a cada 30 minutos para o cartão "Cotações" das TVs. O Banco
/// Central publica boletins ao longo do dia (abertura, intermediários e o
/// fechamento); aqui fica o último.
/// </summary>
public class Cotacao : BaseEntity
{
    /// <summary>Cotação mais velha que isto não vai para as TVs (cobre fim de semana e feriado).</summary>
    public static readonly TimeSpan Validade = TimeSpan.FromDays(4);

    /// <summary>Moedas do cartão, na ordem de exibição.</summary>
    public static readonly IReadOnlyList<(string Codigo, string Nome)> Moedas = [("USD", "Dólar"), ("EUR", "Euro")];

    /// <summary>Código ISO (<c>USD</c>, <c>EUR</c>). Único.</summary>
    public string Moeda { get; set; } = null!;

    public decimal Compra { get; set; }
    public decimal Venda { get; set; }

    /// <summary>Boletim do Banco Central (Abertura, Intermediário, Fechamento PTAX).</summary>
    public string? Boletim { get; set; }

    /// <summary>Data e hora do boletim (Brasília).</summary>
    public DateTimeOffset CotadaEm { get; set; }

    /// <summary>Venda no fechamento do dia útil anterior, para a variação.</summary>
    public decimal? FechamentoAnterior { get; set; }

    public DateTimeOffset AtualizadaEm { get; set; }

    public string? UltimoErro { get; set; }

    /// <summary>Variação da venda contra o fechamento anterior, em %.</summary>
    public decimal? VariacaoPercentual =>
        FechamentoAnterior is { } anterior && anterior != 0 ? Math.Round((Venda - anterior) / anterior * 100, 2) : null;
}
