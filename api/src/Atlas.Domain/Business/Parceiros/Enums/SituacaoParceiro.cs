namespace Atlas.Domain.Business.Parceiros.Enums;

/// <summary>
/// Situação do parceiro na rede. Bloqueado barra a equipe no painel e as telas
/// deixam de receber grade nova (continuam no que já baixaram).
/// </summary>
public enum SituacaoParceiro
{
    Ativo = 1,
    Bloqueado = 2
}
