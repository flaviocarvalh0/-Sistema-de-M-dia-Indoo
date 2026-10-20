using System.Security.Cryptography;
using System.Text;
using Atlas.Domain.Business.Indoor.Entities;

namespace Atlas.Application.Business.Veiculacao;

/// <summary>
/// Leitura de um QR rastreado (<c>/q/{codigo}</c>, anônimo): conta e devolve o
/// destino para o redirecionamento. Nulo = código inexistente (404).
/// </summary>
/// <param name="Codigo">O código do link (maiúsculas ou minúsculas).</param>
/// <param name="Ip">IP de quem leu; só entra no hash diário, nunca é gravado.</param>
/// <param name="Navegador">User-Agent; idem.</param>
public sealed record RegistrarLeituraQrCommand(string Codigo, string? Ip, string? Navegador) : IRequest<Uri?>;

public class RegistrarLeituraQrCommandHandler(IApplicationDbContext context, TimeProvider timeProvider)
    : IRequestHandler<RegistrarLeituraQrCommand, Uri?>
{
    /// <summary>
    /// Segredo do processo: sem ele, dava para testar todos os IPs contra o hash.
    /// Muda quando a API reinicia (a contagem de únicos do dia pode repetir alguém).
    /// </summary>
    private static readonly byte[] Segredo = RandomNumberGenerator.GetBytes(32);

    public async Task<Uri?> Handle(RegistrarLeituraQrCommand request, CancellationToken cancellationToken)
    {
        var codigo = request.Codigo?.Trim().ToUpperInvariant();
        if (codigo is not { Length: Midia.TamanhoCodigoQr })
        {
            return null;
        }

        var midia = await context.Midias
            .Where(m => m.CodigoQr == codigo)
            .Select(m => new { m.Id, m.ClienteId, m.QrConteudo })
            .FirstOrDefaultAsync(cancellationToken);

        // O código só existe nos tipos que abrem link (LinkQr.Rastreavel): o
        // conteúdo é sempre http(s). Mesmo assim, nunca redireciona para outra coisa.
        if (midia?.QrConteudo is null
            || !Uri.TryCreate(midia.QrConteudo, UriKind.Absolute, out var destino)
            || (destino.Scheme != Uri.UriSchemeHttps && destino.Scheme != Uri.UriSchemeHttp))
        {
            return null;
        }

        var agora = timeProvider.GetUtcNow();
        context.LeiturasQr.Add(new LeituraQr
        {
            MidiaId = midia.Id,
            ClienteId = midia.ClienteId,
            Momento = agora,
            Visitante = Visitante(request.Ip, request.Navegador, DateOnly.FromDateTime(agora.UtcDateTime)),
        });
        await context.SaveChangesAsync(cancellationToken);

        return destino;
    }

    /// <summary>Hash diário de IP + navegador: conta leituras únicas por dia sem guardar quem leu.</summary>
    public static string Visitante(string? ip, string? navegador, DateOnly dia)
    {
        var texto = $"{ip}|{navegador}|{dia:yyyy-MM-dd}";
        var hash = HMACSHA256.HashData(Segredo, Encoding.UTF8.GetBytes(texto));
        return Convert.ToHexStringLower(hash)[..LeituraQr.TamanhoVisitante];
    }
}
