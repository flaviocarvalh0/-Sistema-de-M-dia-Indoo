namespace Atlas.Domain.Business.Clientes;

/// <summary>
/// Registro que pertence a um cliente. Os handlers do painel só enxergam os
/// registros do cliente do usuário logado (<c>IClienteAtual</c>).
/// </summary>
public interface IDoCliente
{
    long ClienteId { get; }
}
