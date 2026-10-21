using Atlas.Application.Business.Veiculacao;
using Atlas.Domain.Business.Indoor.Entities;
using Atlas.Domain.Business.Indoor.Enums;

namespace Atlas.Application.Business.Diretorio;

/// <summary>
/// Acesso ou toque na página do diretório (anônimo, ADR-027). Só conta o que
/// a página que a pessoa vê de fato oferece: um contato que não está no
/// diretório desta TV (ou "anuncie" sem o WhatsApp da operadora) é ignorado.
/// </summary>
/// <param name="Diretorio">O diretório desta TV (o mesmo do cache que montou a página).</param>
/// <param name="Evento">Acesso à página, toque num contato ou em "Quer anunciar aqui?".</param>
/// <param name="ClienteId">Dono do contato tocado (só em <see cref="EventoDiretorio.Contato"/>).</param>
/// <param name="Contato">Qual contato (só em <see cref="EventoDiretorio.Contato"/>).</param>
/// <param name="Ip">Só entra no hash diário, nunca é gravado.</param>
/// <param name="Navegador">User-Agent; idem.</param>
/// <returns>Falso quando não contou (evento que a página não oferece).</returns>
public sealed record RegistrarInteracaoDiretorioCommand(
    DiretorioPublicoDto Diretorio,
    EventoDiretorio Evento,
    long? ClienteId,
    TipoQrCode? Contato,
    string? Ip,
    string? Navegador) : IRequest<bool>;

public class RegistrarInteracaoDiretorioCommandHandler(IApplicationDbContext context, TimeProvider timeProvider)
    : IRequestHandler<RegistrarInteracaoDiretorioCommand, bool>
{
    public async Task<bool> Handle(RegistrarInteracaoDiretorioCommand request, CancellationToken cancellationToken)
    {
        var diretorio = request.Diretorio;
        var valido = request.Evento switch
        {
            EventoDiretorio.Acesso => true,
            EventoDiretorio.Anuncie => diretorio.LinkAnuncie is not null,
            EventoDiretorio.Contato => request.ClienteId is { } clienteId
                && request.Contato is { } contato
                && diretorio.Anunciantes.Any(a => a.ClienteId == clienteId && a.Contatos.Any(c => c.Tipo == contato)),
            _ => false,
        };

        if (!valido || diretorio.TelaId == 0)
        {
            return false;
        }

        var agora = timeProvider.GetUtcNow();
        var ehContato = request.Evento == EventoDiretorio.Contato;
        context.InteracoesDiretorio.Add(new InteracaoDiretorio
        {
            TelaId = diretorio.TelaId,
            ParceiroId = diretorio.ParceiroId,
            Evento = request.Evento,
            ClienteId = ehContato ? request.ClienteId : null,
            Contato = ehContato ? request.Contato : null,
            Momento = agora,
            Visitante = RegistrarLeituraQrCommandHandler.Visitante(request.Ip, request.Navegador, DateOnly.FromDateTime(agora.UtcDateTime)),
        });
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
