using Atlas.Application.Business.Indoor.Common;
using Atlas.Application.Business.Parceiros.Common;
using Atlas.Domain.Business.Constants;

namespace Atlas.Application.Business.Indoor.Telas.Commands.EstadoTela;

[Authorize(Policy = BusinessPermissionKeys.Telas.Update)]
public record AtivarTelaCommand(long Id, uint Version) : IRequest<uint>;

/// <summary>Inativa: a TV pareada deixa de receber grade (continua no que já baixou).</summary>
[Authorize(Policy = BusinessPermissionKeys.Telas.Update)]
public record InativarTelaCommand(long Id, uint Version) : IRequest<uint>;

/// <summary>Apaga a tela; a TV pareada perde o acesso.</summary>
[Authorize(Policy = BusinessPermissionKeys.Telas.Delete)]
public record ExcluirTelaCommand(long Id) : IRequest;

public class AtivarTelaCommandHandler(IApplicationDbContext context, IParceiroAtual parceiroAtual)
    : IRequestHandler<AtivarTelaCommand, uint>
{
    public async Task<uint> Handle(AtivarTelaCommand request, CancellationToken cancellationToken)
    {
        var parceiro = await parceiroAtual.ObterAsync(cancellationToken);
        var tela = await RegistrosDoParceiro.CarregarAsync(context.Telas, parceiro.ParceiroId, request.Id, cancellationToken);

        context.ConferirVersao(tela, request.Version);
        tela.Ativo = true;

        return await context.SalvarAsync(tela, cancellationToken);
    }
}

public class InativarTelaCommandHandler(IApplicationDbContext context, IParceiroAtual parceiroAtual)
    : IRequestHandler<InativarTelaCommand, uint>
{
    public async Task<uint> Handle(InativarTelaCommand request, CancellationToken cancellationToken)
    {
        var parceiro = await parceiroAtual.ObterAsync(cancellationToken);
        var tela = await RegistrosDoParceiro.CarregarAsync(context.Telas, parceiro.ParceiroId, request.Id, cancellationToken);

        context.ConferirVersao(tela, request.Version);
        tela.Ativo = false;
        tela.CodigoPareamento = null;
        tela.CodigoPareamentoExpiraEm = null;

        return await context.SalvarAsync(tela, cancellationToken);
    }
}

public class ExcluirTelaCommandHandler(IApplicationDbContext context, IParceiroAtual parceiroAtual)
    : IRequestHandler<ExcluirTelaCommand>
{
    public async Task Handle(ExcluirTelaCommand request, CancellationToken cancellationToken)
    {
        var parceiro = await parceiroAtual.ObterAsync(cancellationToken);
        var tela = await RegistrosDoParceiro.CarregarAsync(
            context.Telas.Include(t => t.Estado), parceiro.ParceiroId, request.Id, cancellationToken);

        context.Telas.Remove(tela);
        await context.SaveChangesAsync(cancellationToken);
    }
}
