using Atlas.Application.Business.Indoor.Armazenamento;
using Atlas.Application.Business.Indoor.Common;
using Atlas.Application.Business.Indoor.Midias.Common;
using Atlas.Application.Business.Clientes.Common;
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
    IClienteAtual clienteAtual,
    IArmazenamentoMidias armazenamento,
    ILogger<CriarMidiaCommandHandler> logger)
    : IRequestHandler<CriarMidiaCommand, long>
{
    public async Task<long> Handle(CriarMidiaCommand request, CancellationToken cancellationToken)
    {
        var cliente = await clienteAtual.ObterAsync(cancellationToken);

        var arquivo = await RecebimentoArquivo.ConferirAsync(
            context, armazenamento, logger, cliente.ClienteId, request.Chave, request.TipoConteudo, request.Hash,
            bytesQueSaem: 0, cancellationToken);

        // Dentro do plano, nasce ativa; com o limite atingido, nasce inativa
        // (o cliente troca qual delas está no ar).
        var ativa = await LimiteMidiasAtivas.TemVagaAsync(context, cliente.ClienteId, cancellationToken);

        var midia = new Midia
        {
            ClienteId = cliente.ClienteId,
            Titulo = request.Titulo.Trim(),
            Tipo = TiposArquivoMidia.TipoDaMidia(request.TipoConteudo),
            ChaveArmazenamento = request.Chave,
            TipoConteudo = request.TipoConteudo,
            TamanhoBytes = arquivo.TamanhoBytes,
            Hash = arquivo.Hash!,
            DuracaoSegundos = request.DuracaoSegundos,
            InicioExibicao = request.InicioExibicao,
            FimExibicao = request.FimExibicao,
            Ativo = ativa,
        };

        context.Midias.Add(midia);
        await context.SaveChangesAsync(cancellationToken);

        return midia.Id;
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
