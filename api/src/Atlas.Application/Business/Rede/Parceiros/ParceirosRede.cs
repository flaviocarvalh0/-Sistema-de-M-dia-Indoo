using Atlas.Application.Business.Common;
using Atlas.Application.Business.Indoor.Common;
using Atlas.Application.Common.Extensions.Pagination;
using Atlas.Application.Common.Models.Pagination;
using Atlas.Domain.Business.Constants;
using Atlas.Domain.Business.Parceiros.Entities;

namespace Atlas.Application.Business.Rede.Parceiros;

// Parceiros: os locais onde a operadora instala as TVs. Não acessam o
// sistema; se também anunciam, são cadastrados como cliente e ligados aqui.

public sealed record ParceiroDto : IDtoAuditavel
{
    public long Id { get; init; }
    public string Nome { get; init; } = null!;
    public string? NomeResponsavel { get; init; }
    public string? Telefone { get; init; }
    public string? Email { get; init; }
    public string? Documento { get; init; }
    public string? Endereco { get; init; }
    public string? Cidade { get; init; }
    public string FusoHorario { get; init; } = null!;
    public bool Ativo { get; init; }
    public string? Observacao { get; init; }

    /// <summary>Cadastro de cliente do dono do local, quando ele também anuncia.</summary>
    public long? ClienteId { get; init; }
    public string? ClienteNome { get; init; }

    /// <summary>TVs instaladas aqui.</summary>
    public int QuantidadeTelas { get; init; }

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
            Telefone = p.Telefone,
            Email = p.Email,
            Documento = p.Documento,
            Endereco = p.Endereco,
            Cidade = p.Cidade,
            FusoHorario = p.FusoHorario,
            Ativo = p.Ativo,
            Observacao = p.Observacao,
            ClienteId = p.ClienteId,
            ClienteNome = p.Cliente != null ? p.Cliente.Nome : null,
            QuantidadeTelas = context.Telas.Count(t => t.ParceiroId == p.Id),
            Created = p.Created,
            CreatedBy = p.CreatedBy,
            LastModified = p.LastModified,
            LastModifiedBy = p.LastModifiedBy,
            Version = EF.Property<uint>(p, "Version"),
        });
}

// ── Consultas ──────────────────────────────────────────────────────────

[Authorize(Policy = BusinessPermissionKeys.Parceiros.View)]
public record GetParceirosQuery : IRequest<PaginatedList<ParceiroDto>>
{
    public PaginationParams PaginationParams { get; init; } = new();
    public string? SearchTerm { get; init; }
    public bool? Ativo { get; init; }
}

public class GetParceirosQueryHandler(IApplicationDbContext context, IIdentityService identityService)
    : IRequestHandler<GetParceirosQuery, PaginatedList<ParceiroDto>>
{
    public async Task<PaginatedList<ParceiroDto>> Handle(GetParceirosQuery request, CancellationToken cancellationToken)
    {
        var query = context.Parceiros.AsNoTracking();

        if (request.Ativo is { } ativo)
        {
            query = query.Where(p => p.Ativo == ativo);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var termo = request.SearchTerm.Trim().ToLower();
            var digitos = DocumentosBrasil.SomenteDigitos(termo);

            query = query.Where(p =>
                p.Nome.ToLower().Contains(termo)
                || (p.NomeResponsavel != null && p.NomeResponsavel.ToLower().Contains(termo))
                || (p.Cidade != null && p.Cidade.ToLower().Contains(termo))
                || (p.Endereco != null && p.Endereco.ToLower().Contains(termo))
                || (digitos.Length > 0 && p.Telefone != null && p.Telefone.Contains(digitos))
                || (digitos.Length > 0 && p.Documento != null && p.Documento.Contains(digitos)));
        }

        var pagina = await query
            .OrderBy(p => p.Nome)
            .ThenBy(p => p.Id)
            .ParaDto(context)
            .ToEntityPaginatedListAsync(context, "parceiro", request.PaginationParams, cancellationToken);

        await identityService.PreencherNomesAsync(pagina.Items, cancellationToken);

        return pagina;
    }
}

[Authorize(Policy = BusinessPermissionKeys.Parceiros.View)]
public record GetParceiroByIdQuery(long Id) : IRequest<ParceiroDto>;

public class GetParceiroByIdQueryHandler(IApplicationDbContext context, IIdentityService identityService)
    : IRequestHandler<GetParceiroByIdQuery, ParceiroDto>
{
    public async Task<ParceiroDto> Handle(GetParceiroByIdQuery request, CancellationToken cancellationToken)
    {
        var parceiro = await context.Parceiros
            .AsNoTracking()
            .Where(p => p.Id == request.Id)
            .ParaDto(context)
            .FirstOrDefaultAsync(cancellationToken);

        Guard.Against.NotFound(request.Id, parceiro);

        await identityService.PreencherNomesAsync([parceiro], cancellationToken);

        return parceiro;
    }
}

// ── Cadastro e edição ──────────────────────────────────────────────────

public interface IDadosParceiro
{
    string Nome { get; }
    string? NomeResponsavel { get; }
    string? Telefone { get; }
    string? Email { get; }
    string? Documento { get; }
    string? Endereco { get; }
    string? Cidade { get; }
    string FusoHorario { get; }
    string? Observacao { get; }
    long? ClienteId { get; }
}

[Authorize(Policy = BusinessPermissionKeys.Parceiros.Create)]
public record CriarParceiroCommand : IRequest<long>, IDadosParceiro
{
    public string Nome { get; init; } = string.Empty;
    public string? NomeResponsavel { get; init; }
    public string? Telefone { get; init; }
    public string? Email { get; init; }
    public string? Documento { get; init; }
    public string? Endereco { get; init; }
    public string? Cidade { get; init; }
    public string FusoHorario { get; init; } = FusosHorarios.Padrao;
    public string? Observacao { get; init; }
    public long? ClienteId { get; init; }
}

[Authorize(Policy = BusinessPermissionKeys.Parceiros.Update)]
public record AtualizarParceiroCommand : IRequest<uint>, IDadosParceiro
{
    public long Id { get; init; }
    public string Nome { get; init; } = string.Empty;
    public string? NomeResponsavel { get; init; }
    public string? Telefone { get; init; }
    public string? Email { get; init; }
    public string? Documento { get; init; }
    public string? Endereco { get; init; }
    public string? Cidade { get; init; }
    public string FusoHorario { get; init; } = FusosHorarios.Padrao;
    public string? Observacao { get; init; }
    public long? ClienteId { get; init; }
    public uint Version { get; init; }
}

public class CriarParceiroCommandHandler(IApplicationDbContext context) : IRequestHandler<CriarParceiroCommand, long>
{
    public async Task<long> Handle(CriarParceiroCommand request, CancellationToken cancellationToken)
    {
        await RegrasParceiro.GarantirClienteAsync(context, request.ClienteId, cancellationToken);

        var parceiro = new Parceiro();
        RegrasParceiro.Aplicar(parceiro, request);
        context.Parceiros.Add(parceiro);

        await context.SaveChangesAsync(cancellationToken);
        return parceiro.Id;
    }
}

public class AtualizarParceiroCommandHandler(IApplicationDbContext context) : IRequestHandler<AtualizarParceiroCommand, uint>
{
    public async Task<uint> Handle(AtualizarParceiroCommand request, CancellationToken cancellationToken)
    {
        var parceiro = await context.Parceiros.FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);
        Guard.Against.NotFound(request.Id, parceiro);

        if (request.ClienteId != parceiro.ClienteId)
        {
            await RegrasParceiro.GarantirClienteAsync(context, request.ClienteId, cancellationToken);
        }

        context.ConferirVersao(parceiro, request.Version);
        RegrasParceiro.Aplicar(parceiro, request);

        return await context.SalvarAsync(parceiro, cancellationToken);
    }
}

internal static class RegrasParceiro
{
    public static void Aplicar(Parceiro parceiro, IDadosParceiro dados)
    {
        parceiro.Nome = dados.Nome.Trim();
        parceiro.NomeResponsavel = RegistrosDoCliente.Limpar(dados.NomeResponsavel);
        parceiro.Telefone = RegistrosDoCliente.Limpar(DocumentosBrasil.SomenteDigitos(dados.Telefone));
        parceiro.Email = RegistrosDoCliente.Limpar(dados.Email)?.ToLowerInvariant();
        parceiro.Documento = DocumentosBrasil.NormalizarDocumento(dados.Documento);
        parceiro.Endereco = RegistrosDoCliente.Limpar(dados.Endereco);
        parceiro.Cidade = RegistrosDoCliente.Limpar(dados.Cidade);
        parceiro.FusoHorario = dados.FusoHorario.Trim();
        parceiro.Observacao = RegistrosDoCliente.Limpar(dados.Observacao);
        parceiro.ClienteId = dados.ClienteId;
    }

    public static async Task GarantirClienteAsync(IApplicationDbContext context, long? clienteId, CancellationToken cancellationToken)
    {
        if (clienteId is { } id && !await context.Clientes.AnyAsync(c => c.Id == id, cancellationToken))
        {
            throw new ValidationException(nameof(IDadosParceiro.ClienteId), ["Cliente não encontrado."]);
        }
    }

    public static void Validar<T>(AbstractValidator<T> validador)
        where T : IDadosParceiro
    {
        validador.RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("O nome do parceiro é obrigatório.")
            .MaximumLength(150).WithMessage("O nome do parceiro deve ter no máximo 150 caracteres.");

        validador.RuleFor(x => x.NomeResponsavel)
            .MaximumLength(150).WithMessage("O nome do responsável deve ter no máximo 150 caracteres.");

        validador.RuleFor(x => x.Telefone)
            .Must(t => string.IsNullOrWhiteSpace(t) || DocumentosBrasil.TelefoneValido(t))
            .WithMessage("Informe um telefone válido com DDD.");

        validador.RuleFor(x => x.Email)
            .MaximumLength(254).WithMessage("O e-mail deve ter no máximo 254 caracteres.")
            .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email)).WithMessage("Informe um e-mail válido.");

        validador.RuleFor(x => x.Documento)
            .Must(d => string.IsNullOrWhiteSpace(d) || DocumentosBrasil.DocumentoValido(d))
            .WithMessage("Informe um CPF ou CNPJ válido.");

        validador.RuleFor(x => x.Endereco)
            .MaximumLength(250).WithMessage("O endereço deve ter no máximo 250 caracteres.");

        validador.RuleFor(x => x.Cidade)
            .MaximumLength(100).WithMessage("A cidade deve ter no máximo 100 caracteres.");

        validador.RuleFor(x => x.FusoHorario)
            .Must(FusosHorarios.Valido)
            .WithMessage("Selecione um fuso horário válido (ex.: America/Sao_Paulo).");
    }
}

public class CriarParceiroCommandValidator : AbstractValidator<CriarParceiroCommand>
{
    public CriarParceiroCommandValidator() => RegrasParceiro.Validar(this);
}

public class AtualizarParceiroCommandValidator : AbstractValidator<AtualizarParceiroCommand>
{
    public AtualizarParceiroCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0).WithMessage("O ID do parceiro é obrigatório.");
        RegrasParceiro.Validar(this);
    }
}

// ── Ativar, inativar e excluir ─────────────────────────────────────────

/// <summary>Inativo sai da lista de escolha das telas; as TVs já instaladas seguem como estão.</summary>
[Authorize(Policy = BusinessPermissionKeys.Parceiros.Update)]
public record DefinirAtivoParceiroCommand(long Id, bool Ativo, uint Version) : IRequest<uint>;

public class DefinirAtivoParceiroCommandHandler(IApplicationDbContext context) : IRequestHandler<DefinirAtivoParceiroCommand, uint>
{
    public async Task<uint> Handle(DefinirAtivoParceiroCommand request, CancellationToken cancellationToken)
    {
        var parceiro = await context.Parceiros.FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);
        Guard.Against.NotFound(request.Id, parceiro);

        context.ConferirVersao(parceiro, request.Version);
        parceiro.Ativo = request.Ativo;

        return await context.SalvarAsync(parceiro, cancellationToken);
    }
}

/// <summary>Só sem TVs: com TV instalada, mova ou exclua as telas antes (ou inative o parceiro).</summary>
[Authorize(Policy = BusinessPermissionKeys.Parceiros.Delete)]
public record ExcluirParceiroCommand(long Id) : IRequest;

public class ExcluirParceiroCommandHandler(IApplicationDbContext context) : IRequestHandler<ExcluirParceiroCommand>
{
    public async Task Handle(ExcluirParceiroCommand request, CancellationToken cancellationToken)
    {
        var parceiro = await context.Parceiros.FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);
        Guard.Against.NotFound(request.Id, parceiro);

        var telas = await context.Telas.CountAsync(t => t.ParceiroId == parceiro.Id, cancellationToken);
        if (telas > 0)
        {
            throw new ValidationException(nameof(ExcluirParceiroCommand.Id),
                [$"{parceiro.Nome} tem {telas} tela(s). Mova as telas para outro parceiro ou exclua-as antes; ou inative o parceiro."]);
        }

        var clima = await context.PrevisoesClima.FirstOrDefaultAsync(p => p.ParceiroId == parceiro.Id, cancellationToken);
        if (clima is not null)
        {
            context.PrevisoesClima.Remove(clima);
        }

        context.Parceiros.Remove(parceiro);
        await context.SaveChangesAsync(cancellationToken);
    }
}
