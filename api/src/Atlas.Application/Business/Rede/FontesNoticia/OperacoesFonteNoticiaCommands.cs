using Atlas.Application.Business.Conteudo.Noticias;
using Atlas.Domain.Business.Constants;

namespace Atlas.Application.Business.Rede.FontesNoticia;

/// <summary>Apaga a fonte, as notícias dela e as imagens guardadas no storage.</summary>
[Authorize(Policy = BusinessPermissionKeys.FonteNoticias.Delete)]
public record ExcluirFonteNoticiaCommand(long Id) : IRequest;

/// <summary>Lê o feed agora, sem esperar a coleta automática (para conferir um feed novo).</summary>
[Authorize(Policy = BusinessPermissionKeys.FonteNoticias.Update)]
public record ColetarFonteNoticiaCommand(long Id) : IRequest<ResultadoColetaNoticias>;

public class ExcluirFonteNoticiaCommandHandler(IApplicationDbContext context, ColetorNoticias coletor)
    : IRequestHandler<ExcluirFonteNoticiaCommand>
{
    public async Task Handle(ExcluirFonteNoticiaCommand request, CancellationToken cancellationToken)
    {
        var fonte = await RegrasFonteNoticia.CarregarAsync(context, request.Id, cancellationToken);

        var noticias = await context.Noticias.Where(n => n.FonteNoticiaId == fonte.Id).ToListAsync(cancellationToken);
        var chaves = noticias.Where(n => n.ChaveImagem is not null).Select(n => n.ChaveImagem!).ToList();

        context.Noticias.RemoveRange(noticias);
        context.FontesNoticia.Remove(fonte);
        await context.SaveChangesAsync(cancellationToken);

        await coletor.RemoverArquivosAsync(chaves, cancellationToken);
    }
}

public class ColetarFonteNoticiaCommandHandler(IApplicationDbContext context, ColetorNoticias coletor)
    : IRequestHandler<ColetarFonteNoticiaCommand, ResultadoColetaNoticias>
{
    public async Task<ResultadoColetaNoticias> Handle(ColetarFonteNoticiaCommand request, CancellationToken cancellationToken)
    {
        // 404 para fonte inexistente; inativa também pode ser conferida.
        await RegrasFonteNoticia.CarregarAsync(context, request.Id, cancellationToken);

        return await coletor.ColetarAsync(request.Id, cancellationToken);
    }
}
