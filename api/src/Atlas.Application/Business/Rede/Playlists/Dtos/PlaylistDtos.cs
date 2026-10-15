using Atlas.Application.Business.Common;
using Atlas.Domain.Business.Indoor.Entities;
using Atlas.Domain.Business.Indoor.Enums;

namespace Atlas.Application.Business.Rede.Playlists.Dtos;

public sealed record PlaylistDto : IDtoAuditavel
{
    public long Id { get; init; }
    public string Nome { get; init; } = null!;
    public string? Descricao { get; init; }
    public bool Ativo { get; init; }
    public int QuantidadeItens { get; init; }

    /// <summary>Telas que exibem esta playlist.</summary>
    public int QuantidadeTelas { get; init; }

    /// <summary>Soma da duração dos itens (um ciclo do loop, sem filtrar dia nem validade).</summary>
    public int DuracaoTotalSegundos { get; init; }

    /// <summary>Uma notícia depois de cada N anúncios (0 = sem notícias).</summary>
    public int NoticiaACadaAnuncios { get; init; }
    public int DuracaoNoticiaSegundos { get; init; }

    /// <summary>Segundos de anúncios por volta (carrossel); 0 = todos em toda volta.</summary>
    public int DuracaoMaximaVoltaSegundos { get; init; }
    public bool EmbaralharAnuncios { get; init; }
    public bool ExibirRelogio { get; init; }
    public bool ExibirClima { get; init; }
    public bool CartaoTempo { get; init; }
    public bool CartaoCotacoes { get; init; }

    /// <summary>Itens na ordem do loop. Só no detalhe (<c>GET /{id}</c>).</summary>
    public IReadOnlyList<ItemPlaylistDto>? Itens { get; init; }

    public DateTimeOffset Created { get; init; }
    public long? CreatedBy { get; init; }
    public string? CreatedByName { get; set; }
    public DateTimeOffset LastModified { get; init; }
    public long? LastModifiedBy { get; init; }
    public string? LastModifiedByName { get; set; }
    public uint Version { get; init; }
}

public sealed record ItemPlaylistDto
{
    public long Id { get; init; }
    public int Ordem { get; init; }
    public long MidiaId { get; init; }
    public string Titulo { get; init; } = null!;

    /// <summary>Anunciante dono da mídia.</summary>
    public long ParceiroId { get; init; }
    public string ParceiroNome { get; init; } = null!;
    public TipoMidia Tipo { get; init; }
    public int DuracaoSegundos { get; init; }

    /// <summary>Bits dos dias (domingo = 1, segunda = 2, … sábado = 64; 127 = todos), como no contrato da TV.</summary>
    public int DiasSemana { get; init; }

    /// <summary>Não entra no sorteio quando a playlist embaralha.</summary>
    public bool PosicaoFixa { get; init; }

    /// <summary>Mídia inativa continua no lugar, mas não toca.</summary>
    public bool MidiaAtiva { get; init; }

    public DateOnly? InicioExibicao { get; init; }
    public DateOnly? FimExibicao { get; init; }
}

public static class PlaylistProjecao
{
    public static IQueryable<PlaylistDto> ParaDto(this IQueryable<Playlist> query, IApplicationDbContext context) =>
        query.Select(p => new PlaylistDto
        {
            Id = p.Id,
            Nome = p.Nome,
            Descricao = p.Descricao,
            Ativo = p.Ativo,
            QuantidadeItens = p.Itens.Count,
            QuantidadeTelas = context.Telas.Count(t => t.PlaylistId == p.Id),
            DuracaoTotalSegundos = p.Itens.Sum(i => (int?)i.Midia.DuracaoSegundos) ?? 0,
            NoticiaACadaAnuncios = p.NoticiaACadaAnuncios,
            DuracaoNoticiaSegundos = p.DuracaoNoticiaSegundos,
            DuracaoMaximaVoltaSegundos = p.DuracaoMaximaVoltaSegundos,
            EmbaralharAnuncios = p.EmbaralharAnuncios,
            ExibirRelogio = p.ExibirRelogio,
            ExibirClima = p.ExibirClima,
            CartaoTempo = p.CartaoTempo,
            CartaoCotacoes = p.CartaoCotacoes,
            Created = p.Created,
            CreatedBy = p.CreatedBy,
            LastModified = p.LastModified,
            LastModifiedBy = p.LastModifiedBy,
            Version = EF.Property<uint>(p, "Version"),
        });

    public static IQueryable<ItemPlaylistDto> ParaDto(this IQueryable<ItemPlaylist> query) =>
        query
            .OrderBy(i => i.Ordem)
            .Select(i => new ItemPlaylistDto
            {
                Id = i.Id,
                Ordem = i.Ordem,
                MidiaId = i.MidiaId,
                Titulo = i.Midia.Titulo,
                ParceiroId = i.Midia.ParceiroId,
                ParceiroNome = i.Midia.Parceiro.Nome,
                Tipo = i.Midia.Tipo,
                DuracaoSegundos = i.Midia.DuracaoSegundos,
                DiasSemana = (int)i.DiasSemana,
                PosicaoFixa = i.PosicaoFixa,
                MidiaAtiva = i.Midia.Ativo,
                InicioExibicao = i.Midia.InicioExibicao,
                FimExibicao = i.Midia.FimExibicao,
            });
}
