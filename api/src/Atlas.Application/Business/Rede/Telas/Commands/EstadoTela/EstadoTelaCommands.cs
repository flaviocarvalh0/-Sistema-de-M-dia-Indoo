using Atlas.Application.Business.Indoor.Common;
using Atlas.Application.Business.Rede.Telas.Commands.SalvarTela;
using Atlas.Domain.Business.Constants;

namespace Atlas.Application.Business.Rede.Telas.Commands.EstadoTela;

[Authorize(Policy = BusinessPermissionKeys.Telas.Update)]
public record AtivarTelaCommand(long Id, uint Version) : IRequest<uint>;

/// <summary>Inativa: a TV pareada deixa de receber grade (continua no que já baixou).</summary>
[Authorize(Policy = BusinessPermissionKeys.Telas.Update)]
public record InativarTelaCommand(long Id, uint Version) : IRequest<uint>;

/// <summary>Apaga a tela; a TV pareada perde o acesso.</summary>
[Authorize(Policy = BusinessPermissionKeys.Telas.Delete)]
public record ExcluirTelaCommand(long Id) : IRequest;

public class AtivarTelaCommandHandler(IApplicationDbContext context)
    : IRequestHandler<AtivarTelaCommand, uint>
{
    public async Task<uint> Handle(AtivarTelaCommand request, CancellationToken cancellationToken)
    {
        var tela = await RegrasTela.CarregarAsync(context, request.Id, cancellationToken);

        context.ConferirVersao(tela, request.Version);
        tela.Ativo = true;

        return await context.SalvarAsync(tela, cancellationToken);
    }
}

public class InativarTelaCommandHandler(IApplicationDbContext context)
    : IRequestHandler<InativarTelaCommand, uint>
{
    public async Task<uint> Handle(InativarTelaCommand request, CancellationToken cancellationToken)
    {
        var tela = await RegrasTela.CarregarAsync(context, request.Id, cancellationToken);

        context.ConferirVersao(tela, request.Version);
        tela.Ativo = false;
        tela.CodigoPareamento = null;
        tela.CodigoPareamentoExpiraEm = null;

        return await context.SalvarAsync(tela, cancellationToken);
    }
}

public class ExcluirTelaCommandHandler(IApplicationDbContext context)
    : IRequestHandler<ExcluirTelaCommand>
{
    public async Task Handle(ExcluirTelaCommand request, CancellationToken cancellationToken)
    {
        var tela = await context.Telas.Include(t => t.Estado).FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);
        Guard.Against.NotFound(request.Id, tela);

        context.Telas.Remove(tela);
        await context.SaveChangesAsync(cancellationToken);
    }
}
