using Atlas.Domain.Business.Clientes.Entities;
using Atlas.Application.Business.Common;
using Atlas.Application.Business.Rede.Clientes.Commands.Common;
using Atlas.Domain.Business.Constants;

namespace Atlas.Application.Business.Rede.Clientes.Commands.AtualizarCliente;

/// <summary>
/// Edição do cadastro pela operadora. O e-mail não muda aqui: é o login do
/// responsável e se altera na conta do usuário (Atlas Studio).
/// </summary>
[Authorize(Policy = BusinessPermissionKeys.Clientes.Update)]
public record AtualizarClienteCommand : IRequest<uint>
{
    public long Id { get; init; }
    public string Nome { get; init; } = string.Empty;
    public string? NomeResponsavel { get; init; }
    public string? Telefone { get; init; }
    public string? Documento { get; init; }
    public string? Cidade { get; init; }
    public string FusoHorario { get; init; } = FusosHorarios.Padrao;
    public int CotaArmazenamentoMb { get; init; }

    /// <summary>Plano: mídias ativas ao mesmo tempo. Baixar não inativa nada (direito adquirido).</summary>
    public int LimiteMidiasAtivas { get; init; } = Cliente.LimiteMidiasAtivasPadrao;
    public string? ObservacaoInterna { get; init; }
    public uint Version { get; init; }
}

public class AtualizarClienteCommandHandler(IApplicationDbContext context)
    : IRequestHandler<AtualizarClienteCommand, uint>
{
    public async Task<uint> Handle(AtualizarClienteCommand request, CancellationToken cancellationToken)
    {
        var cliente = await context.Clientes
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, cliente);

        context.Entry(cliente).Property<uint>("Version").OriginalValue = request.Version;

        cliente.Nome = request.Nome.Trim();
        cliente.NomeResponsavel = Limpar(request.NomeResponsavel);
        cliente.Telefone = Limpar(DocumentosBrasil.SomenteDigitos(request.Telefone));
        cliente.Documento = DocumentosBrasil.NormalizarDocumento(request.Documento);
        cliente.Cidade = Limpar(request.Cidade);
        cliente.FusoHorario = request.FusoHorario.Trim();
        cliente.CotaArmazenamentoMb = request.CotaArmazenamentoMb;
        cliente.LimiteMidiasAtivas = request.LimiteMidiasAtivas;
        cliente.ObservacaoInterna = Limpar(request.ObservacaoInterna);

        await context.SaveChangesAsync(cancellationToken);

        return context.Entry(cliente).Property<uint>("Version").CurrentValue;
    }

    private static string? Limpar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}

public class AtualizarClienteCommandValidator : AbstractValidator<AtualizarClienteCommand>
{
    public AtualizarClienteCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("O ID do cliente é obrigatório.");

        RuleFor(x => x.Nome).NomeCliente();

        RuleFor(x => x.NomeResponsavel)
            .MaximumLength(150).WithMessage("O nome do responsável deve ter no máximo 150 caracteres.");

        RuleFor(x => x.Telefone).TelefoneCliente();
        RuleFor(x => x.Documento).DocumentoCliente();
        RuleFor(x => x.Cidade).CidadeCliente();
        RuleFor(x => x.FusoHorario).FusoHorarioCliente();
        RuleFor(x => x.CotaArmazenamentoMb).CotaCliente();
        RuleFor(x => x.LimiteMidiasAtivas).LimiteMidiasAtivas();

        RuleFor(x => x.Version)
            .GreaterThan(0u).WithMessage("A versão do registro é obrigatória.");
    }
}
