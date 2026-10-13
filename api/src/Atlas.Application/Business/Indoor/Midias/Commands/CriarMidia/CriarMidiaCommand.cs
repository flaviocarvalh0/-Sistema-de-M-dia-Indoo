using Atlas.Application.Business.Indoor.Armazenamento;
using Atlas.Application.Business.Indoor.Common;
using Atlas.Application.Business.Indoor.Midias.Common;
using Atlas.Application.Business.Parceiros.Common;
using Atlas.Domain.Business.Constants;
using Atlas.Domain.Business.Indoor.Entities;
using Microsoft.Extensions.Logging;

namespace Atlas.Application.Business.Indoor.Midias.Commands.CriarMidia;

/// <summary>
/// Segundo passo do envio: o arquivo já está no storage. A API o lê uma vez e
/// grava a mídia com o tamanho, o tipo e o SHA-256 que ela mesma calculou —
/// é com esse hash que a TV confere o download. Arquivo recusado aqui é
/// apagado do storage, para não ocupar a cota.
/// </summary>
[Authorize(Policy = BusinessPermissionKeys.Midias.Create)]
public record CriarMidiaCommand : IRequest<long>
{
    public string Titulo { get; init; } = string.Empty;

    /// <summary>A chave devolvida pelo <c>PrepararEnvioMidiaCommand</c>.</summary>
    public string Chave { get; init; } = string.Empty;

    public string TipoConteudo { get; init; } = string.Empty;

    /// <summary>
    /// SHA-256 calculado pelo navegador (opcional). Se vier e não bater com o
    /// do arquivo no storage, o envio se corrompeu no caminho e é recusado.
    /// </summary>
    public string? Hash { get; init; }

    /// <summary>Imagem: tempo na tela (padrão 15 s). Vídeo: duração lida pelo navegador.</summary>
    public int DuracaoSegundos { get; init; } = Midia.DuracaoPadraoSegundos;

    public DateOnly? InicioExibicao { get; init; }
    public DateOnly? FimExibicao { get; init; }
}

public class CriarMidiaCommandHandler(
    IApplicationDbContext context,
    IParceiroAtual parceiroAtual,
    IArmazenamentoMidias armazenamento,
    ILogger<CriarMidiaCommandHandler> logger)
    : IRequestHandler<CriarMidiaCommand, long>
{
    public async Task<long> Handle(CriarMidiaCommand request, CancellationToken cancellationToken)
    {
        var parceiro = await parceiroAtual.ObterAsync(cancellationToken);

        // A chave precisa ser uma gerada para este parceiro: sem isto, um
        // parceiro poderia registrar como sua a mídia de outro.
        if (!ChavesArmazenamento.Valida(request.Chave, parceiro.ParceiroId, request.TipoConteudo))
        {
            throw new ValidationException(nameof(request.Chave), ["Envio inválido. Envie o arquivo de novo."]);
        }

        if (await context.Midias.AnyAsync(m => m.ChaveArmazenamento == request.Chave, cancellationToken))
        {
            throw new ValidationException(nameof(request.Chave), ["Este arquivo já foi cadastrado."]);
        }

        ArquivoVerificado arquivo;
        await using (var conteudo = await armazenamento.AbrirLeituraAsync(request.Chave, cancellationToken))
        {
            if (conteudo is null)
            {
                throw new ValidationException(nameof(request.Chave), ["O arquivo não chegou ao storage. Envie de novo."]);
            }

            arquivo = await VerificadorArquivoMidia.VerificarAsync(conteudo, cancellationToken);
        }

        if (arquivo.ExcedeLimite)
        {
            await RecusarAsync(request.Chave, nameof(request.Chave),
                $"O arquivo passa de {Midia.TamanhoMaximoBytes / (1024 * 1024)} MB.", cancellationToken);
        }

        if (arquivo.TipoDetectado != request.TipoConteudo)
        {
            await RecusarAsync(request.Chave, nameof(request.TipoConteudo),
                "O conteúdo do arquivo não é um vídeo MP4 nem uma imagem JPEG ou PNG válida.", cancellationToken);
        }

        if (request.Hash is not null && !string.Equals(request.Hash, arquivo.Hash, StringComparison.OrdinalIgnoreCase))
        {
            await RecusarAsync(request.Chave, nameof(request.Hash),
                "O arquivo chegou diferente do enviado (corrompido no caminho). Envie de novo.", cancellationToken);
        }

        var (usado, cota) = await CotaArmazenamento.ObterAsync(context, parceiro.ParceiroId, cancellationToken);
        if (usado + arquivo.TamanhoBytes > cota)
        {
            await RecusarAsync(request.Chave, nameof(request.Chave), CotaArmazenamento.Mensagem(usado, cota), cancellationToken);
        }

        var midia = new Midia
        {
            ParceiroId = parceiro.ParceiroId,
            Titulo = request.Titulo.Trim(),
            Tipo = TiposArquivoMidia.TipoDaMidia(request.TipoConteudo),
            ChaveArmazenamento = request.Chave,
            TipoConteudo = request.TipoConteudo,
            TamanhoBytes = arquivo.TamanhoBytes,
            Hash = arquivo.Hash!,
            DuracaoSegundos = request.DuracaoSegundos,
            InicioExibicao = request.InicioExibicao,
            FimExibicao = request.FimExibicao,
        };

        context.Midias.Add(midia);
        await context.SaveChangesAsync(cancellationToken);

        return midia.Id;
    }

    private async Task RecusarAsync(string chave, string campo, string mensagem, CancellationToken cancellationToken)
    {
        try
        {
            await armazenamento.RemoverAsync(chave, cancellationToken);
        }
        catch (ArmazenamentoIndisponivelException ex)
        {
            // O arquivo recusado fica órfão no storage; não muda a resposta.
            logger.LogWarning(ex, "Não foi possível apagar do storage o arquivo recusado {Chave}.", chave);
        }

        throw new ValidationException(campo, [mensagem]);
    }
}

public class CriarMidiaCommandValidator : AbstractValidator<CriarMidiaCommand>
{
    public CriarMidiaCommandValidator()
    {
        RuleFor(x => x.Titulo).TituloMidia();

        RuleFor(x => x.Chave)
            .NotEmpty().WithMessage("Envie o arquivo antes de salvar.");

        RuleFor(x => x.TipoConteudo)
            .Must(TiposArquivoMidia.Aceito)
            .WithMessage("Envie um vídeo MP4 ou uma imagem JPEG ou PNG.");

        RuleFor(x => x.Hash)
            .Matches("^[0-9a-fA-F]{64}$").WithMessage("O hash deve ser um SHA-256 em hexadecimal.")
            .When(x => x.Hash is not null);

        RuleFor(x => x.DuracaoSegundos).DuracaoMidia();
        RuleFor(x => x).PeriodoMidia(x => x.InicioExibicao, x => x.FimExibicao);
    }
}
