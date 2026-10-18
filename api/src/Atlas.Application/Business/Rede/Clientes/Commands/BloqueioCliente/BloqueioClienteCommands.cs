using Atlas.Domain.Business.Constants;

namespace Atlas.Application.Business.Rede.Clientes.Commands.BloqueioCliente;

/// <summary>
/// Bloqueia o cliente: a equipe dele perde o acesso ao painel
/// (<c>IClienteAtual</c> recusa cliente bloqueado) e as TVs deixam de
/// receber grade nova — continuam tocando o que já baixaram.
/// </summary>
[Authorize(Policy = BusinessPermissionKeys.Clientes.Update)]
public record BloquearClienteCommand : IRequest<uint>
{
    public long Id { get; init; }
    public string Motivo { get; init; } = string.Empty;
    public uint Version { get; init; }
}

public class BloquearClienteCommandHandler(IApplicationDbContext context)
    : IRequestHandler<BloquearClienteCommand, uint>
{
    public async Task<uint> Handle(BloquearClienteCommand request, CancellationToken cancellationToken)
    {
        var cliente = await context.Clientes
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, cliente);

        if (cliente.Bloqueado)
        {
            throw new ValidationException(nameof(cliente.Situacao), ["O cliente já está bloqueado."]);
        }

        context.Entry(cliente).Property<uint>("Version").OriginalValue = request.Version;

        cliente.Bloquear(request.Motivo);

        await context.SaveChangesAsync(cancellationToken);

        return context.Entry(cliente).Property<uint>("Version").CurrentValue;
    }
}

public class BloquearClienteCommandValidator : AbstractValidator<BloquearClienteCommand>
{
    public BloquearClienteCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("O ID do cliente é obrigatório.");

        RuleFor(x => x.Motivo)
            .NotEmpty().WithMessage("O motivo do bloqueio é obrigatório.")
            .MaximumLength(500).WithMessage("O motivo deve ter no máximo 500 caracteres.");

        RuleFor(x => x.Version)
            .GreaterThan(0u).WithMessage("A versão do registro é obrigatória.");
    }
}

/// <summary>Devolve o cliente à situação Ativo.</summary>
[Authorize(Policy = BusinessPermissionKeys.Clientes.Update)]
public record DesbloquearClienteCommand : IRequest<uint>
{
    public long Id { get; init; }
    public uint Version { get; init; }
}

public class DesbloquearClienteCommandHandler(IApplicationDbContext context)
    : IRequestHandler<DesbloquearClienteCommand, uint>
{
    public async Task<uint> Handle(DesbloquearClienteCommand request, CancellationToken cancellationToken)
    {
        var cliente = await context.Clientes
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, cliente);

        if (!cliente.Bloqueado)
        {
            throw new ValidationException(nameof(cliente.Situacao), ["O cliente não está bloqueado."]);
        }

        context.Entry(cliente).Property<uint>("Version").OriginalValue = request.Version;

        cliente.Desbloquear();

        await context.SaveChangesAsync(cancellationToken);

        return context.Entry(cliente).Property<uint>("Version").CurrentValue;
    }
}

public class DesbloquearClienteCommandValidator : AbstractValidator<DesbloquearClienteCommand>
{
    public DesbloquearClienteCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("O ID do cliente é obrigatório.");

        RuleFor(x => x.Version)
            .GreaterThan(0u).WithMessage("A versão do registro é obrigatória.");
    }
}
