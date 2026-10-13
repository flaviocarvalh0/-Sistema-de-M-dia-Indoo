namespace Atlas.Domain.Business.Parceiros;

/// <summary>
/// Registro que pertence a um parceiro. Os handlers do painel só enxergam os
/// registros do parceiro do usuário logado (<c>IParceiroAtual</c>).
/// </summary>
public interface IDoParceiro
{
    long ParceiroId { get; }
}
