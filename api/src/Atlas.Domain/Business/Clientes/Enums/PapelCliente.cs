namespace Atlas.Domain.Business.Clientes.Enums;

/// <summary>
/// Papel do usuário dentro do cliente. No MVP só existe o Proprietário; o
/// papel Equipe já tem lugar no modelo para quando o dono puder cadastrar equipe.
/// </summary>
public enum PapelCliente
{
    Proprietario = 1,
    Equipe = 2
}
