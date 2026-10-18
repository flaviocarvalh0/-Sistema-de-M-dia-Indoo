using Atlas.Application.Business.Indoor.Armazenamento;
using Atlas.Application.Business.Indoor.Midias.Common;
using Atlas.Application.Business.Indoor.Midias.Dtos;
using Atlas.Application.Business.Clientes.Common;
using Atlas.Domain.Business.Constants;
using Atlas.Domain.Business.Indoor.Entities;

namespace Atlas.Application.Business.Indoor.Midias.Commands.EnvioMidia;

/// <summary>
/// Primeiro passo do envio: a API escolhe a chave, confere tipo, tamanho e
/// cota e devolve a URL assinada. O arquivo vai do navegador direto ao
/// storage; depois o painel chama <c>CriarMidiaCommand</c> com a chave.
/// </summary>
[Authorize(Policy = BusinessPermissionKeys.Midias.Create)]
public record PrepararEnvioMidiaCommand : IRequest<EnvioMidiaDto>
{
    /// <summary>MIME do arquivo (<c>video/mp4</c>, <c>image/jpeg</c>, <c>image/png</c>).</summary>
    public string TipoConteudo { get; init; } = string.Empty;

    /// <summary>Tamanho declarado pelo navegador; o real é conferido no <c>CriarMidiaCommand</c>.</summary>
    public long TamanhoBytes { get; init; }
}

public class PrepararEnvioMidiaCommandHandler(
    IApplicationDbContext context,
    IClienteAtual clienteAtual,
    IArmazenamentoMidias armazenamento)
    : IRequestHandler<PrepararEnvioMidiaCommand, EnvioMidiaDto>
{
    public async Task<EnvioMidiaDto> Handle(PrepararEnvioMidiaCommand request, CancellationToken cancellationToken)
    {
        var cliente = await clienteAtual.ObterAsync(cancellationToken);

        var (usado, cota) = await CotaArmazenamento.ObterAsync(context, cliente.ClienteId, cancellationToken);
        if (usado + request.TamanhoBytes > cota)
        {
            throw new ValidationException(nameof(request.TamanhoBytes), [CotaArmazenamento.Mensagem(usado, cota)]);
        }

        var chave = ChavesArmazenamento.Gerar(cliente.ClienteId, request.TipoConteudo);
        var envio = await armazenamento.PrepararEnvioAsync(chave, cancellationToken);

        return new EnvioMidiaDto(chave, envio.Url, envio.ExpiraEm);
    }
}

public class PrepararEnvioMidiaCommandValidator : AbstractValidator<PrepararEnvioMidiaCommand>
{
    public PrepararEnvioMidiaCommandValidator()
    {
        RuleFor(x => x.TipoConteudo)
            .Must(TiposArquivoMidia.Aceito)
            .WithMessage("Envie um vídeo MP4 ou uma imagem JPEG ou PNG.");

        RuleFor(x => x.TamanhoBytes)
            .InclusiveBetween(1, Midia.TamanhoMaximoBytes)
            .WithMessage($"O arquivo deve ter até {Midia.TamanhoMaximoBytes / (1024 * 1024)} MB.");
    }
}
