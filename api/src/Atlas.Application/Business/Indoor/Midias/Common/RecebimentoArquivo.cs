using Atlas.Application.Business.Indoor.Armazenamento;
using Atlas.Domain.Business.Indoor.Entities;
using Microsoft.Extensions.Logging;

namespace Atlas.Application.Business.Indoor.Midias.Common;

/// <summary>
/// Conferência do arquivo que o navegador enviou direto ao storage, usada ao
/// cadastrar e ao substituir: a API lê o arquivo uma vez e confere tipo real,
/// tamanho, hash e cota. Arquivo recusado é apagado do storage, para não
/// ocupar a cota.
/// </summary>
internal static class RecebimentoArquivo
{
    /// <param name="context">Banco.</param>
    /// <param name="armazenamento">Storage das mídias.</param>
    /// <param name="logger">Log do handler que chama.</param>
    /// <param name="parceiroId">Dono do envio.</param>
    /// <param name="chave">Chave devolvida pelo preparo do envio.</param>
    /// <param name="tipoConteudo">MIME informado.</param>
    /// <param name="hashInformado">SHA-256 do navegador (opcional).</param>
    /// <param name="bytesQueSaem">Ao substituir, o tamanho do arquivo antigo, que deixa de contar na cota.</param>
    /// <param name="cancellationToken">Cancelamento.</param>
    public static async Task<ArquivoVerificado> ConferirAsync(
        IApplicationDbContext context,
        IArmazenamentoMidias armazenamento,
        ILogger logger,
        long parceiroId,
        string chave,
        string tipoConteudo,
        string? hashInformado,
        long bytesQueSaem,
        CancellationToken cancellationToken)
    {
        // A chave precisa ser uma gerada para este parceiro: sem isto, um
        // parceiro poderia registrar como sua a mídia de outro.
        if (!ChavesArmazenamento.Valida(chave, parceiroId, tipoConteudo))
        {
            throw new ValidationException("Chave", ["Envio inválido. Envie o arquivo de novo."]);
        }

        if (await context.Midias.AnyAsync(m => m.ChaveArmazenamento == chave, cancellationToken))
        {
            throw new ValidationException("Chave", ["Este arquivo já foi cadastrado."]);
        }

        ArquivoVerificado arquivo;
        await using (var conteudo = await armazenamento.AbrirLeituraAsync(chave, cancellationToken))
        {
            if (conteudo is null)
            {
                throw new ValidationException("Chave", ["O arquivo não chegou ao storage. Envie de novo."]);
            }

            arquivo = await VerificadorArquivoMidia.VerificarAsync(conteudo, cancellationToken);
        }

        if (arquivo.ExcedeLimite)
        {
            await RecusarAsync(armazenamento, logger, chave, "Chave",
                $"O arquivo passa de {Midia.TamanhoMaximoBytes / (1024 * 1024)} MB.", cancellationToken);
        }

        if (arquivo.TipoDetectado != tipoConteudo)
        {
            await RecusarAsync(armazenamento, logger, chave, "TipoConteudo",
                "O conteúdo do arquivo não é um vídeo MP4 nem uma imagem JPEG ou PNG válida.", cancellationToken);
        }

        if (hashInformado is not null && !string.Equals(hashInformado, arquivo.Hash, StringComparison.OrdinalIgnoreCase))
        {
            await RecusarAsync(armazenamento, logger, chave, "Hash",
                "O arquivo chegou diferente do enviado (corrompido no caminho). Envie de novo.", cancellationToken);
        }

        var (usado, cota) = await CotaArmazenamento.ObterAsync(context, parceiroId, cancellationToken);
        if (usado - bytesQueSaem + arquivo.TamanhoBytes > cota)
        {
            await RecusarAsync(armazenamento, logger, chave, "Chave",
                CotaArmazenamento.Mensagem(usado - bytesQueSaem, cota), cancellationToken);
        }

        return arquivo;
    }

    /// <summary>Apaga do storage sem mudar a resposta se o storage falhar (sobra um órfão, só espaço).</summary>
    public static async Task ApagarAsync(IArmazenamentoMidias armazenamento, ILogger logger, string chave, CancellationToken cancellationToken)
    {
        try
        {
            await armazenamento.RemoverAsync(chave, cancellationToken);
        }
        catch (ArmazenamentoIndisponivelException ex)
        {
            logger.LogWarning(ex, "Não foi possível apagar do storage o arquivo {Chave}.", chave);
        }
    }

    private static async Task RecusarAsync(
        IArmazenamentoMidias armazenamento, ILogger logger, string chave, string campo, string mensagem, CancellationToken cancellationToken)
    {
        await ApagarAsync(armazenamento, logger, chave, cancellationToken);
        throw new ValidationException(campo, [mensagem]);
    }
}
