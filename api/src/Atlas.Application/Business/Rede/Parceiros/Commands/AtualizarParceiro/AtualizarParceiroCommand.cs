using Atlas.Application.Business.Common;
using Atlas.Application.Business.Rede.Parceiros.Commands.Common;
using Atlas.Domain.Business.Constants;

namespace Atlas.Application.Business.Rede.Parceiros.Commands.AtualizarParceiro;

/// <summary>
/// Edição do cadastro pela operadora. O e-mail não muda aqui: é o login do
/// responsável e se altera na conta do usuário (Atlas Studio).
/// </summary>
[Authorize(Policy = BusinessPermissionKeys.Parceiros.Update)]
public record AtualizarParceiroCommand : IRequest<uint>
{
    public long Id { get; init; }
    public string Nome { get; init; } = string.Empty;
    public string? NomeResponsavel { get; init; }
    public string? Telefone { get; init; }
    public string? Documento { get; init; }
    public string? Cidade { get; init; }
    public string FusoHorario { get; init; } = FusosHorarios.Padrao;
    public int CotaArmazenamentoMb { get; init; }
    public string? ObservacaoInterna { get; init; }
    public uint Version { get; init; }
}

public class AtualizarParceiroCommandHandler(IApplicationDbContext context)
    : IRequestHandler<AtualizarParceiroCommand, uint>
{
    public async Task<uint> Handle(AtualizarParceiroCommand request, CancellationToken cancellationToken)
    {
        var parceiro = await context.Parceiros
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, parceiro);

        context.Entry(parceiro).Property<uint>("Version").OriginalValue = request.Version;

        parceiro.Nome = request.Nome.Trim();
        parceiro.NomeResponsavel = Limpar(request.NomeResponsavel);
        parceiro.Telefone = Limpar(DocumentosBrasil.SomenteDigitos(request.Telefone));
        parceiro.Documento = DocumentosBrasil.NormalizarDocumento(request.Documento);
        parceiro.Cidade = Limpar(request.Cidade);
        parceiro.FusoHorario = request.FusoHorario.Trim();
        parceiro.CotaArmazenamentoMb = request.CotaArmazenamentoMb;
        parceiro.ObservacaoInterna = Limpar(request.ObservacaoInterna);

        await context.SaveChangesAsync(cancellationToken);

        return context.Entry(parceiro).Property<uint>("Version").CurrentValue;
    }

    private static string? Limpar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}

public class AtualizarParceiroCommandValidator : AbstractValidator<AtualizarParceiroCommand>
{
    public AtualizarParceiroCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("O ID do parceiro é obrigatório.");

        RuleFor(x => x.Nome).NomeParceiro();

        RuleFor(x => x.NomeResponsavel)
            .MaximumLength(150).WithMessage("O nome do responsável deve ter no máximo 150 caracteres.");

        RuleFor(x => x.Telefone).TelefoneParceiro();
        RuleFor(x => x.Documento).DocumentoParceiro();
        RuleFor(x => x.Cidade).CidadeParceiro();
        RuleFor(x => x.FusoHorario).FusoHorarioParceiro();
        RuleFor(x => x.CotaArmazenamentoMb).CotaParceiro();

        RuleFor(x => x.Version)
            .GreaterThan(0u).WithMessage("A versão do registro é obrigatória.");
    }
}
