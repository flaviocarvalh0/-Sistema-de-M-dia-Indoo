using System.Globalization;
using Atlas.Application.Business.Common;
using Atlas.Application.Business.Indoor.Armazenamento;
using Atlas.Application.Business.Indoor.Common;
using Atlas.Application.Business.Indoor.Midias.Common;
using Atlas.Application.Platform.EmailMessages.Common;
using Atlas.Domain.Business.Constants;
using Atlas.Domain.Business.Indoor.Entities;
using Microsoft.Extensions.Logging;

namespace Atlas.Application.Business.Rede.Midias.Commands;

/// <summary>
/// A operadora aprova a troca de arquivo: o arquivo novo vale e as TVs o
/// baixam na próxima sincronização (o hash muda a versão da grade). O antigo
/// sai do storage (ADR-020).
/// </summary>
[Authorize(Policy = BusinessPermissionKeys.Playlists.Update)]
public sealed record AprovarTrocaArquivoCommand(long Id, uint Version) : IRequest<uint>;

/// <summary>A operadora recusa a troca: o arquivo atual segue no ar e o anunciante vê o motivo.</summary>
[Authorize(Policy = BusinessPermissionKeys.Playlists.Update)]
public sealed record RecusarTrocaArquivoCommand : IRequest<uint>
{
    public long Id { get; init; }
    public string Motivo { get; init; } = string.Empty;
    public uint Version { get; init; }
}

public class AprovarTrocaArquivoCommandHandler(
    IApplicationDbContext context,
    IArmazenamentoMidias armazenamento,
    IEmailMessageService emails,
    IConfiguracaoIndoor configuracao,
    ILogger<AprovarTrocaArquivoCommandHandler> logger)
    : IRequestHandler<AprovarTrocaArquivoCommand, uint>
{
    public async Task<uint> Handle(AprovarTrocaArquivoCommand request, CancellationToken cancellationToken)
    {
        var midia = await TrocaArquivo.CarregarComPendenteAsync(context, request.Id, cancellationToken);

        context.ConferirVersao(midia, request.Version);
        var antiga = midia.AprovarTroca();
        await TrocaArquivo.AvisarParceiroAsync(context, emails, configuracao, midia,
            "Troca de arquivo aprovada",
            $"O arquivo novo de \"{midia.Titulo}\" foi aprovado. As TVs passam a exibi-lo em alguns minutos.",
            cancellationToken);

        var versao = await context.SalvarAsync(midia, cancellationToken);
        await RecebimentoArquivo.ApagarAsync(armazenamento, logger, antiga, cancellationToken);

        return versao;
    }
}

public class RecusarTrocaArquivoCommandHandler(
    IApplicationDbContext context,
    IArmazenamentoMidias armazenamento,
    IEmailMessageService emails,
    IConfiguracaoIndoor configuracao,
    ILogger<RecusarTrocaArquivoCommandHandler> logger)
    : IRequestHandler<RecusarTrocaArquivoCommand, uint>
{
    public async Task<uint> Handle(RecusarTrocaArquivoCommand request, CancellationToken cancellationToken)
    {
        var midia = await TrocaArquivo.CarregarComPendenteAsync(context, request.Id, cancellationToken);

        context.ConferirVersao(midia, request.Version);
        var pendente = midia.RecusarTroca(request.Motivo);
        await TrocaArquivo.AvisarParceiroAsync(context, emails, configuracao, midia,
            "Troca de arquivo recusada",
            $"O arquivo novo de \"{midia.Titulo}\" não foi aprovado. Motivo: {midia.MotivoRecusaTroca} As TVs seguem com o arquivo anterior.",
            cancellationToken);

        var versao = await context.SalvarAsync(midia, cancellationToken);
        await RecebimentoArquivo.ApagarAsync(armazenamento, logger, pendente, cancellationToken);

        return versao;
    }
}

public class RecusarTrocaArquivoCommandValidator : AbstractValidator<RecusarTrocaArquivoCommand>
{
    public RecusarTrocaArquivoCommandValidator()
    {
        RuleFor(x => x.Motivo)
            .NotEmpty().WithMessage("Diga ao anunciante por que a troca foi recusada.")
            .MaximumLength(Midia.TamanhoMaximoMotivoRecusa)
            .WithMessage($"O motivo deve ter no máximo {Midia.TamanhoMaximoMotivoRecusa} caracteres.");
        RuleFor(x => x.Version).GreaterThan(0u).WithMessage("A versão do registro é obrigatória.");
    }
}

public class AprovarTrocaArquivoCommandValidator : AbstractValidator<AprovarTrocaArquivoCommand>
{
    public AprovarTrocaArquivoCommandValidator() =>
        RuleFor(x => x.Version).GreaterThan(0u).WithMessage("A versão do registro é obrigatória.");
}

internal static class TrocaArquivo
{
    /// <summary>A mídia (de qualquer parceiro: é a operadora), rastreada; sem troca pendente é 400.</summary>
    public static async Task<Midia> CarregarComPendenteAsync(IApplicationDbContext context, long id, CancellationToken cancellationToken)
    {
        var midia = await context.Midias.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        Guard.Against.NotFound(id, midia);

        if (!midia.TemTrocaPendente)
        {
            throw new ValidationException(nameof(Midia.PendenteChave), ["Esta mídia não tem troca de arquivo aguardando aprovação."]);
        }

        return midia;
    }

    /// <summary>E-mail para o responsável pelo parceiro, na mesma gravação.</summary>
    public static async Task AvisarParceiroAsync(
        IApplicationDbContext context,
        IEmailMessageService emails,
        IConfiguracaoIndoor configuracao,
        Midia midia,
        string titulo,
        string texto,
        CancellationToken cancellationToken)
    {
        var parceiro = await context.Parceiros
            .Where(p => p.Id == midia.ParceiroId)
            .Select(p => new { p.Nome, p.NomeResponsavel, p.Email })
            .FirstAsync(cancellationToken);

        await emails.EnqueueAsync(new EmailMessageRequest
        {
            Subject = $"{titulo} — {midia.Titulo}",
            HtmlBody = ModeloEmail.Montar(
                previa: texto,
                titulo: titulo,
                motivo: $"Você recebeu este e-mail porque o {parceiro.Nome} trocou o arquivo de uma mídia na Mídia Indoor.",
                ModeloEmail.Paragrafo($"Olá, {parceiro.NomeResponsavel ?? parceiro.Nome}!"),
                ModeloEmail.Paragrafo(texto),
                ModeloEmail.Botao("Abrir minhas mídias", $"{configuracao.UrlPainel.TrimEnd('/')}/indoor/midia")),
            Recipients = [new EmailRecipientRequest { Name = parceiro.NomeResponsavel, Email = parceiro.Email, Type = EmailRecipientType.To }],
            RelatedEntityType = "Midia",
            RelatedEntityId = midia.Id.ToString(CultureInfo.InvariantCulture),
        }, cancellationToken);
    }
}
