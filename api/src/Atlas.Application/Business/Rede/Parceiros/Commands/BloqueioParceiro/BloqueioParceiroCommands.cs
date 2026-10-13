using Atlas.Domain.Business.Constants;

namespace Atlas.Application.Business.Rede.Parceiros.Commands.BloqueioParceiro;

/// <summary>
/// Bloqueia o parceiro: a equipe dele perde o acesso ao painel
/// (<c>IParceiroAtual</c> recusa parceiro bloqueado) e as TVs deixam de
/// receber grade nova — continuam tocando o que já baixaram.
/// </summary>
[Authorize(Policy = BusinessPermissionKeys.Parceiros.Update)]
public record BloquearParceiroCommand : IRequest<uint>
{
    public long Id { get; init; }
    public string Motivo { get; init; } = string.Empty;
    public uint Version { get; init; }
}

public class BloquearParceiroCommandHandler(IApplicationDbContext context)
    : IRequestHandler<BloquearParceiroCommand, uint>
{
    public async Task<uint> Handle(BloquearParceiroCommand request, CancellationToken cancellationToken)
    {
        var parceiro = await context.Parceiros
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, parceiro);

        if (parceiro.Bloqueado)
        {
            throw new ValidationException(nameof(parceiro.Situacao), ["O parceiro já está bloqueado."]);
        }

        context.Entry(parceiro).Property<uint>("Version").OriginalValue = request.Version;

        parceiro.Bloquear(request.Motivo);

        await context.SaveChangesAsync(cancellationToken);

        return context.Entry(parceiro).Property<uint>("Version").CurrentValue;
    }
}

public class BloquearParceiroCommandValidator : AbstractValidator<BloquearParceiroCommand>
{
    public BloquearParceiroCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("O ID do parceiro é obrigatório.");

        RuleFor(x => x.Motivo)
            .NotEmpty().WithMessage("O motivo do bloqueio é obrigatório.")
            .MaximumLength(500).WithMessage("O motivo deve ter no máximo 500 caracteres.");

        RuleFor(x => x.Version)
            .GreaterThan(0u).WithMessage("A versão do registro é obrigatória.");
    }
}

/// <summary>Devolve o parceiro à situação Ativo.</summary>
[Authorize(Policy = BusinessPermissionKeys.Parceiros.Update)]
public record DesbloquearParceiroCommand : IRequest<uint>
{
    public long Id { get; init; }
    public uint Version { get; init; }
}

public class DesbloquearParceiroCommandHandler(IApplicationDbContext context)
    : IRequestHandler<DesbloquearParceiroCommand, uint>
{
    public async Task<uint> Handle(DesbloquearParceiroCommand request, CancellationToken cancellationToken)
    {
        var parceiro = await context.Parceiros
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, parceiro);

        if (!parceiro.Bloqueado)
        {
            throw new ValidationException(nameof(parceiro.Situacao), ["O parceiro não está bloqueado."]);
        }

        context.Entry(parceiro).Property<uint>("Version").OriginalValue = request.Version;

        parceiro.Desbloquear();

        await context.SaveChangesAsync(cancellationToken);

        return context.Entry(parceiro).Property<uint>("Version").CurrentValue;
    }
}

public class DesbloquearParceiroCommandValidator : AbstractValidator<DesbloquearParceiroCommand>
{
    public DesbloquearParceiroCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("O ID do parceiro é obrigatório.");

        RuleFor(x => x.Version)
            .GreaterThan(0u).WithMessage("A versão do registro é obrigatória.");
    }
}
