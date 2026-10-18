using Atlas.Application.Business.Common;
using Atlas.Application.Platform.EmailMessages.Common;
using Atlas.Application.Business.Indoor.Armazenamento;
using Atlas.Application.Business.Indoor.Common;
using Atlas.Application.Business.Indoor.Midias.Common;
using Atlas.Application.Business.Clientes.Common;
using Atlas.Domain.Business.Constants;
using Microsoft.Extensions.Logging;

namespace Atlas.Application.Business.Indoor.Midias.Commands.SubstituirArquivo;

/// <summary>
/// "Atualize uma vez, reflita em todas as telas": troca o arquivo de uma
/// mídia mantendo a mídia, o título e o lugar dela em todas as playlists. O
/// hash novo muda a versão da grade das TVs, que baixam o arquivo novo na
/// próxima sincronização. O envio é igual ao do cadastro (URL assinada e
/// conferência do arquivo); o arquivo antigo sai do storage depois de gravar.
/// </summary>
/// <remarks>
/// Mídia que está em alguma grade (ADR-020): o arquivo novo fica
/// <b>aguardando aprovação</b> da operadora e as TVs seguem com o atual —
/// conteúdo novo não entra no ar sem alguém da rede ver. Mídia fora das
/// grades troca na hora (não toca em lugar nenhum).
/// </remarks>
[Authorize(Policy = BusinessPermissionKeys.Midias.Update)]
public record SubstituirArquivoMidiaCommand : IRequest<SubstituicaoArquivoDto>
{
    public long Id { get; init; }

    /// <summary>A chave devolvida pelo <c>PrepararEnvioMidiaCommand</c>.</summary>
    public string Chave { get; init; } = string.Empty;

    public string TipoConteudo { get; init; } = string.Empty;

    /// <summary>SHA-256 calculado pelo navegador (opcional, conferido).</summary>
    public string? Hash { get; init; }

    /// <summary>Nova duração (o vídeo novo pode ter outra). Nula = mantém.</summary>
    public int? DuracaoSegundos { get; init; }

    public uint Version { get; init; }
}

/// <param name="Version">Nova versão da mídia.</param>
/// <param name="AguardandoAprovacao">A troca foi para a operadora aprovar; as TVs seguem com o arquivo atual.</param>
public sealed record SubstituicaoArquivoDto(uint Version, bool AguardandoAprovacao);

public class SubstituirArquivoMidiaCommandHandler(
    IApplicationDbContext context,
    IClienteAtual clienteAtual,
    IArmazenamentoMidias armazenamento,
    IEmailMessageService emails,
    IConfiguracaoIndoor configuracao,
    TimeProvider timeProvider,
    ILogger<SubstituirArquivoMidiaCommandHandler> logger)
    : IRequestHandler<SubstituirArquivoMidiaCommand, SubstituicaoArquivoDto>
{
    public async Task<SubstituicaoArquivoDto> Handle(SubstituirArquivoMidiaCommand request, CancellationToken cancellationToken)
    {
        var cliente = await clienteAtual.ObterAsync(cancellationToken);
        var midia = await RegistrosDoCliente.CarregarAsync(context.Midias, cliente.ClienteId, request.Id, cancellationToken);
        var emGrade = await context.ItensPlaylist.AnyAsync(i => i.MidiaId == midia.Id, cancellationToken);

        // Troca direta: o arquivo atual (e um pendente esquecido) deixam de contar.
        // Troca para aprovar: o atual continua no ar, só um pendente anterior sai.
        var arquivo = await RecebimentoArquivo.ConferirAsync(
            context, armazenamento, logger, cliente.ClienteId, request.Chave, request.TipoConteudo, request.Hash,
            bytesQueSaem: (emGrade ? 0 : midia.TamanhoBytes) + (midia.PendenteTamanhoBytes ?? 0), cancellationToken);

        var tipo = TiposArquivoMidia.TipoDaMidia(request.TipoConteudo);
        var chavesQueSaem = new List<string>();

        context.ConferirVersao(midia, request.Version);
        if (emGrade)
        {
            if (midia.ProporArquivo(request.Chave, tipo, request.TipoConteudo, arquivo.TamanhoBytes, arquivo.Hash!,
                    request.DuracaoSegundos, timeProvider.GetUtcNow()) is { } pendenteAnterior)
            {
                chavesQueSaem.Add(pendenteAnterior);
            }

            await AvisarOperadoraAsync(midia, cliente.ClienteId, cancellationToken);
        }
        else
        {
            chavesQueSaem.Add(midia.ChaveArmazenamento);
            if (midia.PendenteChave is not null)
            {
                chavesQueSaem.Add(midia.RecusarTroca("Substituída por outro arquivo antes da aprovação."));
            }

            midia.SubstituirArquivo(request.Chave, tipo, request.TipoConteudo, arquivo.TamanhoBytes, arquivo.Hash!);
            if (request.DuracaoSegundos is { } duracao)
            {
                midia.DuracaoSegundos = duracao;
            }
        }

        uint versao;
        try
        {
            versao = await context.SalvarAsync(midia, cancellationToken);
        }
        catch
        {
            // Não gravou (ex.: versão antiga): o arquivo novo não é de ninguém.
            await RecebimentoArquivo.ApagarAsync(armazenamento, logger, request.Chave, CancellationToken.None);
            throw;
        }

        // Depois do banco: se o storage falhar, sobra o arquivo antigo órfão
        // (só espaço), nunca uma mídia apontando para arquivo que não existe.
        foreach (var chave in chavesQueSaem)
        {
            await RecebimentoArquivo.ApagarAsync(armazenamento, logger, chave, cancellationToken);
        }

        return new SubstituicaoArquivoDto(versao, emGrade);
    }

    /// <summary>E-mail para a operadora (se configurado), na mesma gravação da troca.</summary>
    private async Task AvisarOperadoraAsync(Domain.Business.Indoor.Entities.Midia midia, long clienteId, CancellationToken cancellationToken)
    {
        if (configuracao.EmailsOperadora.Count == 0)
        {
            return;
        }

        var nomeCliente = await context.Clientes.Where(p => p.Id == clienteId).Select(p => p.Nome).FirstAsync(cancellationToken);
        var urlPainel = configuracao.UrlPainel.TrimEnd('/');

        await emails.EnqueueAsync(new EmailMessageRequest
        {
            Subject = $"Troca de arquivo para aprovar — {nomeCliente}",
            HtmlBody = ModeloEmail.Montar(
                previa: $"{nomeCliente} enviou um arquivo novo para \"{midia.Titulo}\". As TVs seguem com o atual até você aprovar.",
                titulo: "Troca de arquivo para aprovar",
                motivo: "Você recebeu este e-mail porque está na lista de avisos da operadora (Indoor:Alertas:Emails).",
                ModeloEmail.ParagrafoHtml($"<strong>{ModeloEmail.E(nomeCliente)}</strong> enviou um arquivo novo para a mídia <strong>{ModeloEmail.E(midia.Titulo)}</strong>, que está em grades das TVs."),
                ModeloEmail.Paragrafo("As TVs continuam com o arquivo atual até a troca ser aprovada."),
                ModeloEmail.Botao("Ver e aprovar", $"{urlPainel}/rede/playlist")),
            Recipients = configuracao.EmailsOperadora
                .Select(e => new EmailRecipientRequest { Email = e, Type = EmailRecipientType.To })
                .ToList(),
            RelatedEntityType = "Midia",
            RelatedEntityId = midia.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
        }, cancellationToken);
    }
}

public class SubstituirArquivoMidiaCommandValidator : AbstractValidator<SubstituirArquivoMidiaCommand>
{
    public SubstituirArquivoMidiaCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0).WithMessage("O ID da mídia é obrigatório.");

        RuleFor(x => x.Chave)
            .NotEmpty().WithMessage("Envie o arquivo antes de salvar.");

        RuleFor(x => x.TipoConteudo)
            .Must(TiposArquivoMidia.Aceito)
            .WithMessage("Envie um vídeo MP4 ou uma imagem JPEG ou PNG.");

        RuleFor(x => x.Hash)
            .Matches("^[0-9a-fA-F]{64}$").WithMessage("O hash deve ser um SHA-256 em hexadecimal.")
            .When(x => x.Hash is not null);

        RuleFor(x => x.DuracaoSegundos!.Value).DuracaoMidia().When(x => x.DuracaoSegundos is not null);

        RuleFor(x => x.Version).GreaterThan(0u).WithMessage("A versão do registro é obrigatória.");
    }
}
