using Atlas.Application.Common.Exceptions;
using Atlas.Domain.Business.Parceiros.Enums;

namespace Atlas.Application.Business.Parceiros.Common;

/// <summary>
/// Implementação de <see cref="IParceiroAtual"/>. Registrada com escopo de
/// requisição: a consulta ao banco acontece no máximo uma vez por requisição.
/// </summary>
public sealed class ParceiroAtual : IParceiroAtual
{
    private readonly IUser _usuario;
    private readonly IApplicationDbContext _context;
    private ParceiroDoUsuario? _resolvido;

    public ParceiroAtual(IUser usuario, IApplicationDbContext context)
    {
        _usuario = usuario;
        _context = context;
    }

    public async Task<ParceiroDoUsuario> ObterAsync(CancellationToken cancellationToken)
    {
        if (_resolvido is not null)
        {
            return _resolvido;
        }

        var usuarioId = _usuario.Id
            ?? throw new UnauthorizedAccessException();

        var vinculo = await _context.UsuariosParceiro
            .AsNoTracking()
            .Where(v => v.UsuarioId == usuarioId && v.Ativo)
            .Select(v => new { v.ParceiroId, v.Papel, v.Parceiro.Situacao, v.Parceiro.FusoHorario })
            .FirstOrDefaultAsync(cancellationToken);

        // Sem vínculo e parceiro bloqueado dão o mesmo 403: a resposta não
        // revela se o parceiro existe para quem não pertence a ele.
        if (vinculo is null || vinculo.Situacao == SituacaoParceiro.Bloqueado)
        {
            throw new ForbiddenAccessException();
        }

        _resolvido = new ParceiroDoUsuario(vinculo.ParceiroId, vinculo.Papel, vinculo.FusoHorario);
        return _resolvido;
    }
}
