using Atlas.Domain.Business.Indoor.Enums;
using Atlas.Domain.Platform.Attributes;

namespace Atlas.Domain.Business.Indoor.Entities;

/// <summary>
/// Grade de exibição da rede, montada pela operadora: mídias de vários
/// parceiros (anunciantes) na ordem do loop. Reaproveitável: uma playlist
/// roda em várias telas. A tela aponta para a playlist; a playlist não
/// conhece as telas.
/// </summary>
[MetadataConfiguration("Playlists")]
public class Playlist : BaseAuditableEntity
{
    /// <summary>Único na rede.</summary>
    public string Nome { get; set; } = null!;

    public string? Descricao { get; set; }

    public bool Ativo { get; set; } = true;

    public const int MaximoNoticiaACadaAnuncios = 20;
    public const int DuracaoNoticiaMinimaSegundos = 5;
    public const int DuracaoNoticiaMaximaSegundos = 60;
    public const int DuracaoNoticiaPadraoSegundos = 10;

    /// <summary>
    /// Composição da fila na TV: um informativo (notícia, tempo ou cotações)
    /// depois de cada N anúncios (0 = sem informativos). A TV monta a fila a
    /// cada volta do loop.
    /// </summary>
    public int NoticiaACadaAnuncios { get; set; }

    /// <summary>Tempo de cada notícia na tela.</summary>
    public int DuracaoNoticiaSegundos { get; set; } = DuracaoNoticiaPadraoSegundos;

    /// <summary>
    /// A TV sorteia a ordem dos anúncios a cada volta; os itens com
    /// <see cref="ItemPlaylist.PosicaoFixa"/> ficam no lugar.
    /// </summary>
    public bool EmbaralharAnuncios { get; set; }

    /// <summary>Relógio sobreposto, na hora do estabelecimento (host) da tela.</summary>
    public bool ExibirRelogio { get; set; } = true;

    /// <summary>Temperatura da cidade do estabelecimento, sobreposta (Open-Meteo).</summary>
    public bool ExibirClima { get; set; } = true;

    /// <summary>
    /// Cartão "Previsão do tempo" em tela cheia, no rodízio dos informativos
    /// (entra no lugar de uma notícia a cada N anúncios).
    /// </summary>
    public bool CartaoTempo { get; set; }

    /// <summary>Cartão "Cotações" (dólar e euro, PTAX do Banco Central), no rodízio dos informativos.</summary>
    public bool CartaoCotacoes { get; set; }

    /// <summary>Itens na ordem do loop (<see cref="ItemPlaylist.Ordem"/>).</summary>
    public List<ItemPlaylist> Itens { get; private set; } = [];

    /// <summary>
    /// Deixa os itens iguais aos informados, na ordem informada: a posição na
    /// lista vira a <see cref="ItemPlaylist.Ordem"/>. Com id atualiza, sem id
    /// cria, ausente remove — a mesma mídia pode aparecer mais de uma vez no loop.
    /// </summary>
    /// <exception cref="InvalidOperationException">Id de item que não é desta playlist ou dias vazios.</exception>
    public void DefinirItens(IReadOnlyList<ItemPlaylistInformado> informados)
    {
        var ids = informados.Where(i => i.Id is not null).Select(i => i.Id!.Value).ToHashSet();
        if (ids.Any(id => Itens.All(i => i.Id != id)))
        {
            throw new InvalidOperationException("Item informado não pertence a esta playlist.");
        }

        if (informados.Any(i => (i.DiasSemana & DiasSemana.Todos) == DiasSemana.Nenhum))
        {
            throw new InvalidOperationException("Todo item precisa de pelo menos um dia da semana.");
        }

        Itens.RemoveAll(i => !ids.Contains(i.Id));

        for (var ordem = 0; ordem < informados.Count; ordem++)
        {
            var informado = informados[ordem];
            var item = informado.Id is { } id ? Itens.Single(i => i.Id == id) : new ItemPlaylist();

            item.MidiaId = informado.MidiaId;
            item.DiasSemana = informado.DiasSemana & DiasSemana.Todos;
            item.PosicaoFixa = informado.PosicaoFixa;
            item.Ordem = ordem;

            if (informado.Id is null)
            {
                Itens.Add(item);
            }
        }
    }
}

/// <summary>Item como chega do formulário da playlist.</summary>
public sealed record ItemPlaylistInformado(long? Id, long MidiaId, DiasSemana DiasSemana, bool PosicaoFixa = false);
