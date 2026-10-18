using Atlas.Application.Business.Common;
using Atlas.Domain.Business.Clientes.Entities;
using Atlas.Domain.Business.Clientes.Enums;

namespace Atlas.Application.Business.Rede.Clientes.Dtos;

/// <summary>Cliente como a operadora o vê (inclui a observação interna e o uso da cota).</summary>
public sealed record ClienteDto : IDtoAuditavel
{
    public long Id { get; init; }
    public string Nome { get; init; } = null!;
    public string? NomeResponsavel { get; init; }
    public string Email { get; init; } = null!;
    public string? Telefone { get; init; }
    public string? Documento { get; init; }
    public string? Cidade { get; init; }
    public string FusoHorario { get; init; } = null!;
    public int CotaArmazenamentoMb { get; init; }

    /// <summary>Mídias que o plano permite ativas ao mesmo tempo.</summary>
    public int LimiteMidiasAtivas { get; init; }

    /// <summary>Mídias ativas agora (pode passar do limite: direito adquirido).</summary>
    public int MidiasAtivas { get; init; }

    /// <summary>Soma do tamanho das mídias do cliente (ativas ou não: ocupam o storage igual).</summary>
    public long ArmazenamentoUsadoBytes { get; init; }

    public int QuantidadeTelas { get; init; }
    public SituacaoCliente Situacao { get; init; }

    /// <summary>O cliente aceitou aparecer no diretório das TVs (só ele muda; aqui é leitura).</summary>
    public bool ExibirNoDiretorioPublico { get; init; }

    public DateTimeOffset? DiretorioAceitoEm { get; init; }
    public string? MotivoBloqueio { get; init; }
    public string? ObservacaoInterna { get; init; }
    public DateTimeOffset Created { get; init; }
    public long? CreatedBy { get; init; }
    public string? CreatedByName { get; set; }
    public DateTimeOffset LastModified { get; init; }
    public long? LastModifiedBy { get; init; }
    public string? LastModifiedByName { get; set; }
    public uint Version { get; init; }
}

public static class ClienteProjecao
{
    public static IQueryable<ClienteDto> ParaDto(this IQueryable<Cliente> query, IApplicationDbContext context) =>
        query.Select(p => new ClienteDto
        {
            Id = p.Id,
            Nome = p.Nome,
            NomeResponsavel = p.NomeResponsavel,
            Email = p.Email,
            Telefone = p.Telefone,
            Documento = p.Documento,
            Cidade = p.Cidade,
            FusoHorario = p.FusoHorario,
            CotaArmazenamentoMb = p.CotaArmazenamentoMb,
            LimiteMidiasAtivas = p.LimiteMidiasAtivas,
            MidiasAtivas = context.Midias.Count(m => m.ClienteId == p.Id && m.Ativo),
            ArmazenamentoUsadoBytes = context.Midias.Where(m => m.ClienteId == p.Id).Sum(m => (long?)m.TamanhoBytes) ?? 0,
            // TVs onde os anúncios dele passam agora (mesma regra de "Onde meus anúncios passam").
            QuantidadeTelas = context.Telas.Count(t => t.Ativo && t.Playlist != null && t.Playlist.Ativo
                && t.Playlist.Itens.Any(i => i.Midia.ClienteId == p.Id && i.Midia.Ativo)),
            Situacao = p.Situacao,
            ExibirNoDiretorioPublico = p.ExibirNoDiretorioPublico,
            DiretorioAceitoEm = p.DiretorioAceitoEm,
            MotivoBloqueio = p.MotivoBloqueio,
            ObservacaoInterna = p.ObservacaoInterna,
            Created = p.Created,
            CreatedBy = p.CreatedBy,
            LastModified = p.LastModified,
            LastModifiedBy = p.LastModifiedBy,
            Version = EF.Property<uint>(p, "Version"),
        });
}
