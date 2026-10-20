namespace Atlas.Application.Business.Indoor.Armazenamento;

/// <summary>
/// Storage dos arquivos de mídia (vídeos e imagens). O arquivo nunca passa pela
/// API no envio: o painel recebe uma URL assinada e manda direto ao storage; a
/// API só confere o que chegou. As TVs baixam por URL assinada de leitura.
/// </summary>
/// <remarks>
/// Implementações: Cloudflare R2 (API S3, ADR-025) e Supabase Storage. A
/// interface usa só a chave do objeto: trocar de provedor é configuração.
/// </remarks>
public interface IArmazenamentoMidias
{
    /// <summary>
    /// URL assinada para o navegador enviar o arquivo com <c>PUT</c> (corpo binário).
    /// No R2, o tipo e o tamanho vão assinados: o envio só passa com exatamente
    /// esse <c>Content-Type</c> e esse <c>Content-Length</c>.
    /// </summary>
    Task<EnvioPreparado> PrepararEnvioAsync(string chave, string tipoConteudo, long tamanhoBytes, CancellationToken cancellationToken);

    /// <summary>URL assinada para baixar o arquivo, válida pelo tempo informado.</summary>
    Task<Uri> GerarUrlLeituraAsync(string chave, TimeSpan validade, CancellationToken cancellationToken);

    /// <summary>
    /// URLs assinadas de várias chaves numa chamada só (a sincronização das
    /// TVs). Chave inexistente fica fora do resultado.
    /// </summary>
    Task<IReadOnlyDictionary<string, Uri>> GerarUrlsLeituraAsync(
        IReadOnlyCollection<string> chaves,
        TimeSpan validade,
        CancellationToken cancellationToken);

    /// <summary>Metadados do objeto; <c>null</c> se ele não existe.</summary>
    Task<ObjetoArmazenado?> ObterAsync(string chave, CancellationToken cancellationToken);

    /// <summary>Conteúdo do objeto para a API conferir hash e tipo; <c>null</c> se ele não existe.</summary>
    Task<Stream?> AbrirLeituraAsync(string chave, CancellationToken cancellationToken);

    /// <summary>
    /// Grava o objeto a partir da própria API (sobrescreve se já existe). Usado
    /// só para conteúdo que a API busca sozinha, como as imagens das notícias.
    /// </summary>
    Task GravarAsync(string chave, byte[] conteudo, string tipoConteudo, CancellationToken cancellationToken);

    /// <summary>Apaga o objeto. Não falha se ele já não existe.</summary>
    Task RemoverAsync(string chave, CancellationToken cancellationToken);
}

/// <param name="Url">Endereço para o <c>PUT</c> do arquivo.</param>
/// <param name="ExpiraEm">Até quando o endereço aceita o envio.</param>
public sealed record EnvioPreparado(Uri Url, DateTimeOffset ExpiraEm);

public sealed record ObjetoArmazenado(long TamanhoBytes, string? TipoConteudo);

/// <summary>Um objeto na listagem do bucket (inventário, ADR-025).</summary>
public sealed record ObjetoListado(string Chave, long TamanhoBytes, DateTimeOffset ModificadoEm);

/// <summary>
/// Storage que sabe listar o bucket inteiro (o R2). Com ele o inventário mede
/// o uso real e acha arquivos órfãos; sem ele, vale a soma do banco.
/// </summary>
public interface IListagemArmazenamento
{
    IAsyncEnumerable<ObjetoListado> ListarAsync(CancellationToken cancellationToken);
}

/// <summary>O storage recusou ou não respondeu; a operação pode ser tentada de novo.</summary>
public sealed class ArmazenamentoIndisponivelException(string mensagem, Exception? interna = null)
    : Exception(mensagem, interna);
