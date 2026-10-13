using Atlas.Application.Business.Indoor.Common;
using Atlas.Application.Business.Indoor.Midias.Common;
using Atlas.Application.Business.Parceiros.Common;
using Atlas.Domain.Business.Constants;

namespace Atlas.Application.Business.Indoor.Midias.Commands.AtualizarMidia;

/// <summary>Título, duração e período de exibição. O arquivo não muda: outro arquivo é outra mídia.</summary>
[Authorize(Policy = BusinessPermissionKeys.Midias.Update)]
public record AtualizarMidiaCommand : IRequest<uint>
{
    public long Id { get; init; }
    public string Titulo { get; init; } = string.Empty;
    public int DuracaoSegundos { get; init; }
    public DateOnly? InicioExibicao { get; init; }
    public DateOnly? FimExibicao { get; init; }
    public uint Version { get; init; }
}

public class AtualizarMidiaCommandHandler(IApplicationDbContext context, IParceiroAtual parceiroAtual)
    : IRequestHandler<AtualizarMidiaCommand, uint>
{
    public async Task<uint> Handle(AtualizarMidiaCommand request, CancellationToken cancellationToken)
    {
        var parceiro = await parceiroAtual.ObterAsync(cancellationToken);
        var midia = await RegistrosDoParceiro.CarregarAsync(context.Midias, parceiro.ParceiroId, request.Id, cancellationToken);

        context.ConferirVersao(midia, request.Version);

        midia.Titulo = request.Titulo.Trim();
        midia.DuracaoSegundos = request.DuracaoSegundos;
        midia.InicioExibicao = request.InicioExibicao;
        midia.FimExibicao = request.FimExibicao;

        return await context.SalvarAsync(midia, cancellationToken);
    }
}

public class AtualizarMidiaCommandValidator : AbstractValidator<AtualizarMidiaCommand>
{
    public AtualizarMidiaCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0).WithMessage("O ID da mídia é obrigatório.");
        RuleFor(x => x.Titulo).TituloMidia();
        RuleFor(x => x.DuracaoSegundos).DuracaoMidia();
        RuleFor(x => x).PeriodoMidia(x => x.InicioExibicao, x => x.FimExibicao);
        RuleFor(x => x.Version).GreaterThan(0u).WithMessage("A versão do registro é obrigatória.");
    }
}
