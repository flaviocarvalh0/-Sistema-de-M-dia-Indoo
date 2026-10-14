using Atlas.Application.Business.Indoor.Armazenamento;
using Atlas.Application.Business.Indoor.Common;
using Atlas.Application.Business.Indoor.Midias.Common;
using Atlas.Application.Business.Parceiros.Common;
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
[Authorize(Policy = BusinessPermissionKeys.Midias.Update)]
public record SubstituirArquivoMidiaCommand : IRequest<uint>
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

public class SubstituirArquivoMidiaCommandHandler(
    IApplicationDbContext context,
    IParceiroAtual parceiroAtual,
    IArmazenamentoMidias armazenamento,
    ILogger<SubstituirArquivoMidiaCommandHandler> logger)
    : IRequestHandler<SubstituirArquivoMidiaCommand, uint>
{
    public async Task<uint> Handle(SubstituirArquivoMidiaCommand request, CancellationToken cancellationToken)
    {
        var parceiro = await parceiroAtual.ObterAsync(cancellationToken);
        var midia = await RegistrosDoParceiro.CarregarAsync(context.Midias, parceiro.ParceiroId, request.Id, cancellationToken);

        // O arquivo antigo deixa de contar na cota: o novo ocupa o lugar dele.
        var arquivo = await RecebimentoArquivo.ConferirAsync(
            context, armazenamento, logger, parceiro.ParceiroId, request.Chave, request.TipoConteudo, request.Hash,
            bytesQueSaem: midia.TamanhoBytes, cancellationToken);

        var chaveAntiga = midia.ChaveArmazenamento;

        context.ConferirVersao(midia, request.Version);
        midia.SubstituirArquivo(
            request.Chave,
            TiposArquivoMidia.TipoDaMidia(request.TipoConteudo),
            request.TipoConteudo,
            arquivo.TamanhoBytes,
            arquivo.Hash!);

        if (request.DuracaoSegundos is { } duracao)
        {
            midia.DuracaoSegundos = duracao;
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
        await RecebimentoArquivo.ApagarAsync(armazenamento, logger, chaveAntiga, cancellationToken);

        return versao;
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
