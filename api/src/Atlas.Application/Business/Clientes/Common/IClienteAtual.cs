using Atlas.Domain.Business.Clientes.Enums;

namespace Atlas.Application.Business.Clientes.Common;

/// <summary>
/// Cliente do usuário logado — a única fonte do inquilino nas operações do
/// painel (telas, mídias, playlists). O id do cliente nunca vem do cliente.
/// </summary>
public interface IClienteAtual
{
    /// <summary>
    /// Resolve o cliente do usuário logado.
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">Requisição sem usuário autenticado.</exception>
    /// <exception cref="Atlas.Application.Common.Exceptions.ForbiddenAccessException">
    /// Usuário sem vínculo ativo com um cliente, ou cliente bloqueado.
    /// </exception>
    Task<ClienteDoUsuario> ObterAsync(CancellationToken cancellationToken);
}

public sealed record ClienteDoUsuario(long ClienteId, PapelCliente Papel, string FusoHorario);
