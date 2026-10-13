using Atlas.Application.Common.Exceptions;
using Atlas.Domain.Business.Parceiros.Enums;

namespace Atlas.Application.Business.Dispositivo.Common;

/// <summary>Implementação de <see cref="IDispositivoAtual"/>; uma consulta por requisição.</summary>
public sealed class DispositivoAtual(ICredencialDispositivo credencial, IApplicationDbContext context)
    : IDispositivoAtual
{
    private TelaDoDispositivo? _resolvida;

    public async Task<TelaDoDispositivo> ObterAsync(CancellationToken cancellationToken)
    {
        if (_resolvida is not null)
        {
            return _resolvida;
        }

        if (credencial is not { TelaId: { } telaId, ParceiroId: { } parceiroId, DispositivoId: { } dispositivoId, VersaoCredencial: { } versao })
        {
            throw new UnauthorizedAccessException();
        }

        var tela = await context.Telas
            .AsNoTracking()
            .Where(t => t.Id == telaId && t.ParceiroId == parceiroId)
            .Select(t => new { t.DispositivoId, t.VersaoCredencial, t.Ativo, t.Parceiro.Situacao })
            .FirstOrDefaultAsync(cancellationToken);

        // Tela apagada, TV desvinculada ou outra TV pareada no lugar: o token
        // deixou de valer e a TV precisa de um código novo.
        if (tela is null || tela.DispositivoId != dispositivoId || tela.VersaoCredencial != versao)
        {
            throw new UnauthorizedAccessException();
        }

        if (!tela.Ativo || tela.Situacao == SituacaoParceiro.Bloqueado)
        {
            throw new ForbiddenAccessException();
        }

        _resolvida = new TelaDoDispositivo(telaId, parceiroId, dispositivoId, versao, credencial.ExpiraEm);
        return _resolvida;
    }
}
