using Atlas.Domain.Business.Parceiros;
using Atlas.Domain.Common;

namespace Atlas.Application.Business.Indoor.Common;

/// <summary>
/// Carga e gravação dos registros do painel com isolamento por parceiro e
/// concorrência otimista (<c>Version</c> = <c>xmin</c>).
/// </summary>
internal static class RegistrosDoParceiro
{
    /// <summary>
    /// O registro do parceiro, rastreado. De outro parceiro ou inexistente dá
    /// 404 — a resposta não revela que o registro existe.
    /// </summary>
    public static async Task<T> CarregarAsync<T>(
        IQueryable<T> conjunto,
        long parceiroId,
        long id,
        CancellationToken cancellationToken)
        where T : BaseEntity, IDoParceiro
    {
        var registro = await conjunto.FirstOrDefaultAsync(r => r.Id == id && r.ParceiroId == parceiroId, cancellationToken);

        Guard.Against.NotFound(id, registro);

        return registro;
    }

    /// <summary>A gravação só passa se ninguém alterou o registro desde a versão informada (senão, 409).</summary>
    public static void ConferirVersao(this IApplicationDbContext context, BaseEntity registro, uint version) =>
        context.Entry(registro).Property<uint>("Version").OriginalValue = version;

    /// <summary>Grava e devolve a nova versão do registro.</summary>
    public static async Task<uint> SalvarAsync(this IApplicationDbContext context, BaseEntity registro, CancellationToken cancellationToken)
    {
        await context.SaveChangesAsync(cancellationToken);

        return context.Entry(registro).Property<uint>("Version").CurrentValue;
    }

    public static string? Limpar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
