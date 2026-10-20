namespace Atlas.Domain.Business.Indoor.Entities;

/// <summary>
/// Uma exibição de anúncio numa TV, registrada pelo próprio player (prova de
/// veiculação, ADR-023). A TV grava no banco local quando o item sai da tela
/// e envia em lote; o <see cref="IdLocal"/> torna o reenvio idempotente.
/// </summary>
/// <remarks>
/// Sem chave estrangeira para tela ou mídia: o histórico de veiculação do
/// cliente precisa sobreviver à exclusão da TV ou da mídia. Os ids de cliente
/// e parceiro são os do momento do recebimento.
/// </remarks>
public class RegistroExibicao : BaseEntity
{
    /// <summary>Exibição mais longa que isto é descartada (o player limita a duração do item).</summary>
    public static readonly TimeSpan DuracaoMaxima = TimeSpan.FromHours(2);

    public long TelaId { get; set; }
    public long ParceiroId { get; set; }
    public long MidiaId { get; set; }
    public long ClienteId { get; set; }

    /// <summary>Id gerado na TV (UUID): repetido = já recebido.</summary>
    public Guid IdLocal { get; set; }

    /// <summary>Quando o anúncio apareceu na tela (relógio da TV, em UTC).</summary>
    public DateTimeOffset Inicio { get; set; }

    /// <summary>Quanto tempo ficou na tela.</summary>
    public int DuracaoMs { get; set; }

    /// <summary>Foi até o fim (vídeo terminou ou imagem cumpriu o tempo). Falso = interrompido por erro.</summary>
    public bool Concluida { get; set; }

    /// <summary>A TV estava sem conseguir falar com a API quando exibiu (tocou do que já tinha baixado).</summary>
    public bool SemConexao { get; set; }

    public DateTimeOffset RecebidoEm { get; set; }
}
