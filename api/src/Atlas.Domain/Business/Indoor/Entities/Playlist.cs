using Atlas.Domain.Business.Indoor.Enums;
using Atlas.Domain.Business.Parceiros.Entities;
using Atlas.Domain.Platform.Attributes;

namespace Atlas.Domain.Business.Indoor.Entities;

/// <summary>
/// Grade de exibição do parceiro, reaproveitável: uma playlist roda em várias
/// telas (as 5 TVs da academia tocam a mesma grade). A tela aponta para a
/// playlist; a playlist não conhece as telas.
/// </summary>
[MetadataConfiguration("Playlists")]
public class Playlist : BaseAuditableEntity
{
    public long ParceiroId { get; set; }
    public Parceiro Parceiro { get; set; } = null!;

    /// <summary>Único por parceiro.</summary>
    public string Nome { get; set; } = null!;

    public string? Descricao { get; set; }

    public bool Ativo { get; set; } = true;

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
            item.Ordem = ordem;

            if (informado.Id is null)
            {
                Itens.Add(item);
            }
        }
    }
}

/// <summary>Item como chega do formulário da playlist.</summary>
public sealed record ItemPlaylistInformado(long? Id, long MidiaId, DiasSemana DiasSemana);
