using Atlas.Domain.Business.Indoor.Enums;

namespace Atlas.Domain.Business.Indoor.Entities;

/// <summary>
/// Posição de uma mídia no loop da playlist. Sem tela própria: é gravado junto
/// com a playlist (<see cref="Playlist.DefinirItens"/>).
/// </summary>
public class ItemPlaylist : BaseEntity
{
    public long PlaylistId { get; set; }

    public long MidiaId { get; set; }
    public Midia Midia { get; set; } = null!;

    /// <summary>Posição no loop, a partir de zero; menor toca primeiro.</summary>
    public int Ordem { get; set; }

    public DiasSemana DiasSemana { get; set; } = DiasSemana.Todos;

    /// <summary>
    /// Fica sempre nesta posição quando a playlist embaralha os anúncios
    /// (ex.: o anúncio do próprio estabelecimento abrindo o loop).
    /// </summary>
    public bool PosicaoFixa { get; set; }
}
