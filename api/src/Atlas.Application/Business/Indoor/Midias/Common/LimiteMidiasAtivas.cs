namespace Atlas.Application.Business.Indoor.Midias.Common;

/// <summary>
/// O plano do cliente: quantas mídias ele pode ter ativas ao mesmo tempo
/// (<c>Cliente.LimiteMidiasAtivas</c>). Vale ao ativar e ao cadastrar; quem
/// já estava acima quando o limite mudou continua como está (direito
/// adquirido) até inativar alguma.
/// </summary>
internal static class LimiteMidiasAtivas
{
    public static async Task<(int Ativas, int Limite)> ObterAsync(
        IApplicationDbContext context,
        long clienteId,
        CancellationToken cancellationToken)
    {
        var ativas = await context.Midias.CountAsync(m => m.ClienteId == clienteId && m.Ativo, cancellationToken);
        var limite = await context.Clientes
            .Where(p => p.Id == clienteId)
            .Select(p => p.LimiteMidiasAtivas)
            .FirstAsync(cancellationToken);

        return (ativas, limite);
    }

    public static async Task<bool> TemVagaAsync(IApplicationDbContext context, long clienteId, CancellationToken cancellationToken)
    {
        var (ativas, limite) = await ObterAsync(context, clienteId, cancellationToken);
        return ativas < limite;
    }

    /// <summary>400 quando ativar mais uma passa do plano.</summary>
    public static async Task GarantirVagaAsync(IApplicationDbContext context, long clienteId, CancellationToken cancellationToken)
    {
        var (ativas, limite) = await ObterAsync(context, clienteId, cancellationToken);
        if (ativas >= limite)
        {
            throw new ValidationException("Ativo", [Mensagem(ativas, limite)]);
        }
    }

    public static string Mensagem(int ativas, int limite) =>
        $"Seu plano permite {limite} mídia{(limite == 1 ? "" : "s")} ativa{(limite == 1 ? "" : "s")} ao mesmo tempo e você já tem {ativas}. " +
        "Inative uma (ou substitua o arquivo dela) ou fale com a operadora para ampliar o plano.";
}
