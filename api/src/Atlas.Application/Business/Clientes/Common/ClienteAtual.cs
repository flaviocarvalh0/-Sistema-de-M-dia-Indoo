using Atlas.Application.Common.Exceptions;
using Atlas.Domain.Business.Clientes.Enums;

namespace Atlas.Application.Business.Clientes.Common;

/// <summary>
/// Implementação de <see cref="IClienteAtual"/>. Registrada com escopo de
/// requisição: a consulta ao banco acontece no máximo uma vez por requisição.
/// </summary>
public sealed class ClienteAtual : IClienteAtual
{
    private readonly IUser _usuario;
    private readonly IApplicationDbContext _context;
    private ClienteDoUsuario? _resolvido;

    public ClienteAtual(IUser usuario, IApplicationDbContext context)
    {
        _usuario = usuario;
        _context = context;
    }

    public async Task<ClienteDoUsuario> ObterAsync(CancellationToken cancellationToken)
    {
        if (_resolvido is not null)
        {
            return _resolvido;
        }

        var usuarioId = _usuario.Id
            ?? throw new UnauthorizedAccessException();

        var vinculo = await _context.UsuariosCliente
            .AsNoTracking()
            .Where(v => v.UsuarioId == usuarioId && v.Ativo)
            .Select(v => new { v.ClienteId, v.Papel, v.Cliente.Situacao, v.Cliente.FusoHorario })
            .FirstOrDefaultAsync(cancellationToken);

        // Sem vínculo e cliente bloqueado dão o mesmo 403: a resposta não
        // revela se o cliente existe para quem não pertence a ele.
        if (vinculo is null || vinculo.Situacao == SituacaoCliente.Bloqueado)
        {
            throw new ForbiddenAccessException();
        }

        _resolvido = new ClienteDoUsuario(vinculo.ClienteId, vinculo.Papel, vinculo.FusoHorario);
        return _resolvido;
    }
}
