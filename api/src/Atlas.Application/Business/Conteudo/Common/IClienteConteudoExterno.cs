namespace Atlas.Application.Business.Conteudo.Common;

/// <summary>
/// Leitura de conteúdo público fora da rede (feeds de notícias, imagens das
/// notícias e a API de clima), com limite de tamanho e tempo.
/// </summary>
public interface IClienteConteudoExterno
{
    /// <summary>
    /// GET no endereço. Lança <see cref="ConteudoExternoIndisponivelException"/>
    /// se não responder, responder com erro ou passar de <paramref name="limiteBytes"/>
    /// — com <paramref name="truncar"/>, devolve só o começo em vez de falhar
    /// (basta para ler o &lt;head&gt; de uma página).
    /// </summary>
    Task<ConteudoBaixado> BaixarAsync(Uri url, long limiteBytes, CancellationToken cancellationToken, bool truncar = false);
}

public sealed record ConteudoBaixado(byte[] Bytes, string? TipoConteudo);

public sealed class ConteudoExternoIndisponivelException(string mensagem, Exception? interna = null)
    : Exception(mensagem, interna);
