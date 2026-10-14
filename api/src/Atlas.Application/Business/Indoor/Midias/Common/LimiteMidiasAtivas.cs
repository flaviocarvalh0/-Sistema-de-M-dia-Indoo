namespace Atlas.Application.Business.Indoor.Midias.Common;

/// <summary>
/// O plano do parceiro: quantas mídias ele pode ter ativas ao mesmo tempo
/// (<c>Parceiro.LimiteMidiasAtivas</c>). Vale ao ativar e ao cadastrar; quem
/// já estava acima quando o limite mudou continua como está (direito
/// adquirido) até inativar alguma.
/// </summary>
internal static class LimiteMidiasAtivas
{
    public static async Task<(int Ativas, int Limite)> ObterAsync(
        IApplicationDbContext context,
        long parceiroId,
        CancellationToken cancellationToken)
    {
        var ativas = await context.Midias.CountAsync(m => m.ParceiroId == parceiroId && m.Ativo, cancellationToken);
        var limite = await context.Parceiros
            .Where(p => p.Id == parceiroId)
            .Select(p => p.LimiteMidiasAtivas)
            .FirstAsync(cancellationToken);

        return (ativas, limite);
    }

    public static async Task<bool> TemVagaAsync(IApplicationDbContext context, long parceiroId, CancellationToken cancellationToken)
    {
        var (ativas, limite) = await ObterAsync(context, parceiroId, cancellationToken);
        return ativas < limite;
    }

    /// <summary>400 quando ativar mais uma passa do plano.</summary>
    public static async Task GarantirVagaAsync(IApplicationDbContext context, long parceiroId, CancellationToken cancellationToken)
    {
        var (ativas, limite) = await ObterAsync(context, parceiroId, cancellationToken);
        if (ativas >= limite)
        {
            throw new ValidationException("Ativo", [Mensagem(ativas, limite)]);
        }
    }

    public static string Mensagem(int ativas, int limite) =>
        $"Seu plano permite {limite} mídia{(limite == 1 ? "" : "s")} ativa{(limite == 1 ? "" : "s")} ao mesmo tempo e você já tem {ativas}. " +
        "Inative uma (ou substitua o arquivo dela) ou fale com a operadora para ampliar o plano.";
}
