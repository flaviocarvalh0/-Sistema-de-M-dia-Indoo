namespace Atlas.Domain.Business.Clientes.Enums;

/// <summary>
/// Situação do cliente na rede. Bloqueado barra a equipe no painel e as telas
/// deixam de receber grade nova (continuam no que já baixaram).
/// </summary>
public enum SituacaoCliente
{
    Ativo = 1,
    Bloqueado = 2
}
