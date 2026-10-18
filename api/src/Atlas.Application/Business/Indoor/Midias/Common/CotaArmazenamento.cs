namespace Atlas.Application.Business.Indoor.Midias.Common;

/// <summary>
/// Espaço que o cliente ocupa no storage: soma das mídias gravadas, ativas
/// ou não (inativar não libera espaço; excluir libera), mais os arquivos de
/// troca que aguardam aprovação.
/// </summary>
internal static class CotaArmazenamento
{
    public static async Task<(long UsadoBytes, long CotaBytes)> ObterAsync(
        IApplicationDbContext context,
        long clienteId,
        CancellationToken cancellationToken)
    {
        var usado = await context.Midias
            .Where(m => m.ClienteId == clienteId)
            .SumAsync(m => (long?)m.TamanhoBytes + (m.PendenteTamanhoBytes ?? 0), cancellationToken) ?? 0;

        var cotaMb = await context.Clientes
            .Where(p => p.Id == clienteId)
            .Select(p => p.CotaArmazenamentoMb)
            .FirstAsync(cancellationToken);

        return (usado, cotaMb * 1024L * 1024L);
    }

    public static string Mensagem(long usadoBytes, long cotaBytes) =>
        $"O arquivo não cabe no espaço disponível: {Mb(cotaBytes - usadoBytes)} livres de {Mb(cotaBytes)}. Exclua mídias que não usa mais ou peça mais espaço à operadora.";

    private static string Mb(long bytes) => $"{Math.Max(0, bytes) / (1024.0 * 1024.0):0.#} MB";
}
