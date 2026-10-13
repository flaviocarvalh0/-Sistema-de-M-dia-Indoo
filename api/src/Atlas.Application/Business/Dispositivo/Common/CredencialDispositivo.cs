namespace Atlas.Application.Business.Dispositivo.Common;

/// <summary>
/// Claims do token da TV. A TV não é um usuário da plataforma: o token não
/// tem <c>NameIdentifier</c> nem sessão, e só vale nas rotas do dispositivo
/// (esquema de autenticação próprio, audiência própria).
/// </summary>
public static class ClaimsDispositivo
{
    public const string TelaId = "indoor:tela";
    public const string ParceiroId = "indoor:parceiro";
    public const string DispositivoId = "indoor:dispositivo";
    public const string VersaoCredencial = "indoor:credencial";
}

/// <summary>O que veio no token da requisição (lido pela camada web).</summary>
public interface ICredencialDispositivo
{
    long? TelaId { get; }
    long? ParceiroId { get; }
    string? DispositivoId { get; }
    int? VersaoCredencial { get; }

    /// <summary>Validade do token (claim <c>exp</c>), para a sincronização decidir se renova.</summary>
    DateTimeOffset? ExpiraEm { get; }
}

/// <summary>Emite o token da TV.</summary>
public interface IGeradorTokenDispositivo
{
    TokenDispositivo Gerar(long telaId, long parceiroId, string dispositivoId, int versaoCredencial);
}

/// <param name="Token">JWT para o cabeçalho <c>Authorization: Bearer</c>.</param>
/// <param name="ExpiraEm">A sincronização entrega um novo antes disso.</param>
public sealed record TokenDispositivo(string Token, DateTimeOffset ExpiraEm);

/// <summary>
/// A tela da TV que fez a requisição, conferida no banco a cada chamada:
/// credencial revogada (TV desvinculada ou outra pareada) dá 401 — a TV volta
/// à tela de pareamento; tela inativa ou parceiro bloqueado dá 403 — a TV
/// continua tocando o que já baixou.
/// </summary>
public interface IDispositivoAtual
{
    Task<TelaDoDispositivo> ObterAsync(CancellationToken cancellationToken);
}

public sealed record TelaDoDispositivo(
    long TelaId,
    long ParceiroId,
    string DispositivoId,
    int VersaoCredencial,
    DateTimeOffset? ExpiraEmToken);
