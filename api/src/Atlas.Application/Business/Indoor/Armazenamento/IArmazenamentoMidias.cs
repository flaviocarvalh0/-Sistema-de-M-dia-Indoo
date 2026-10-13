namespace Atlas.Application.Business.Indoor.Armazenamento;

/// <summary>
/// Storage dos arquivos de mídia (vídeos e imagens). O arquivo nunca passa pela
/// API no envio: o painel recebe uma URL assinada e manda direto ao storage; a
/// API só confere o que chegou. As TVs baixam por URL assinada de leitura.
/// </summary>
/// <remarks>
/// Implementação atual: Supabase Storage (bucket privado). A interface usa só
/// a chave do objeto, para a troca de provedor (S3, R2) ser de configuração.
/// </remarks>
public interface IArmazenamentoMidias
{
    /// <summary>URL assinada para o navegador enviar o arquivo com <c>PUT</c> (corpo binário).</summary>
    Task<EnvioPreparado> PrepararEnvioAsync(string chave, CancellationToken cancellationToken);

    /// <summary>URL assinada para baixar o arquivo, válida pelo tempo informado.</summary>
    Task<Uri> GerarUrlLeituraAsync(string chave, TimeSpan validade, CancellationToken cancellationToken);

    /// <summary>Metadados do objeto; <c>null</c> se ele não existe.</summary>
    Task<ObjetoArmazenado?> ObterAsync(string chave, CancellationToken cancellationToken);

    /// <summary>Conteúdo do objeto para a API conferir hash e tipo; <c>null</c> se ele não existe.</summary>
    Task<Stream?> AbrirLeituraAsync(string chave, CancellationToken cancellationToken);

    /// <summary>Apaga o objeto. Não falha se ele já não existe.</summary>
    Task RemoverAsync(string chave, CancellationToken cancellationToken);
}

/// <param name="Url">Endereço para o <c>PUT</c> do arquivo.</param>
/// <param name="ExpiraEm">Até quando o endereço aceita o envio.</param>
public sealed record EnvioPreparado(Uri Url, DateTimeOffset ExpiraEm);

public sealed record ObjetoArmazenado(long TamanhoBytes, string? TipoConteudo);

/// <summary>O storage recusou ou não respondeu; a operação pode ser tentada de novo.</summary>
public sealed class ArmazenamentoIndisponivelException(string mensagem, Exception? interna = null)
    : Exception(mensagem, interna);
