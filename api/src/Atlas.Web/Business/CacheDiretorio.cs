using Atlas.Application.Business.Diretorio;
using Microsoft.Extensions.Caching.Memory;

namespace Atlas.Web.Business;

/// <summary>
/// O diretório de cada código fica 60 s em memória: um QR na TV de uma
/// academia lotada vira uma consulta por minuto, não uma por celular. Código
/// inexistente também fica guardado (como "não existe"), para quem tenta
/// adivinhar códigos não chegar ao banco.
/// </summary>
/// <remarks>
/// Desligar o diretório ou o aceite de um cliente vale em até 60 s.
/// </remarks>
public sealed class CacheDiretorio(IMemoryCache cache)
{
    public static readonly TimeSpan Validade = TimeSpan.FromSeconds(60);

    /// <summary>Nulo = não existe (ou desligado).</summary>
    public async Task<DiretorioPublicoDto?> ObterAsync(ISender sender, string codigo, CancellationToken cancellationToken)
    {
        var normalizado = GetDiretorioPublicoQueryHandler.NormalizarCodigo(codigo);
        if (normalizado is null)
        {
            return null;
        }

        var chave = "diretorio:" + normalizado;
        if (cache.TryGetValue(chave, out DiretorioPublicoDto? guardado))
        {
            return guardado;
        }

        DiretorioPublicoDto? diretorio;
        try
        {
            diretorio = await sender.Send(new GetDiretorioPublicoQuery(normalizado), cancellationToken);
        }
        catch (NotFoundException)
        {
            diretorio = null;
        }

        cache.Set(chave, diretorio, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = Validade, Size = 1 });

        return diretorio;
    }
}
