using Atlas.Application.Business.Common;
using Atlas.Domain.Business.Parceiros.Entities;
using Atlas.Domain.Business.Parceiros.Enums;

namespace Atlas.Application.Business.Rede.Parceiros.Dtos;

/// <summary>Parceiro como a operadora o vê (inclui a observação interna e o uso da cota).</summary>
public sealed record ParceiroDto : IDtoAuditavel
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

    /// <summary>Soma do tamanho das mídias do parceiro (ativas ou não: ocupam o storage igual).</summary>
    public long ArmazenamentoUsadoBytes { get; init; }

    public int QuantidadeTelas { get; init; }
    public SituacaoParceiro Situacao { get; init; }
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

public static class ParceiroProjecao
{
    public static IQueryable<ParceiroDto> ParaDto(this IQueryable<Parceiro> query, IApplicationDbContext context) =>
        query.Select(p => new ParceiroDto
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
            ArmazenamentoUsadoBytes = context.Midias.Where(m => m.ParceiroId == p.Id).Sum(m => (long?)m.TamanhoBytes) ?? 0,
            QuantidadeTelas = context.Telas.Count(t => t.ParceiroId == p.Id),
            Situacao = p.Situacao,
            MotivoBloqueio = p.MotivoBloqueio,
            ObservacaoInterna = p.ObservacaoInterna,
            Created = p.Created,
            CreatedBy = p.CreatedBy,
            LastModified = p.LastModified,
            LastModifiedBy = p.LastModifiedBy,
            Version = EF.Property<uint>(p, "Version"),
        });
}
