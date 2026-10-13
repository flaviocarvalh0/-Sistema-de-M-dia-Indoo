namespace Atlas.Domain.Business.Parceiros.Enums;

/// <summary>
/// Papel do usuário dentro do parceiro. No MVP só existe o Proprietário; o
/// papel Equipe já tem lugar no modelo para quando o dono puder cadastrar equipe.
/// </summary>
public enum PapelParceiro
{
    Proprietario = 1,
    Equipe = 2
}
