using Atlas.Domain.Business.Parceiros.Enums;

namespace Atlas.Application.Business.Parceiros.Common;

/// <summary>
/// Parceiro do usuário logado — a única fonte do inquilino nas operações do
/// painel (telas, mídias, playlists). O id do parceiro nunca vem do cliente.
/// </summary>
public interface IParceiroAtual
{
    /// <summary>
    /// Resolve o parceiro do usuário logado.
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">Requisição sem usuário autenticado.</exception>
    /// <exception cref="Atlas.Application.Common.Exceptions.ForbiddenAccessException">
    /// Usuário sem vínculo ativo com um parceiro, ou parceiro bloqueado.
    /// </exception>
    Task<ParceiroDoUsuario> ObterAsync(CancellationToken cancellationToken);
}

public sealed record ParceiroDoUsuario(long ParceiroId, PapelParceiro Papel, string FusoHorario);
