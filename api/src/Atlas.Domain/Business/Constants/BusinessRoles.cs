namespace Atlas.Domain.Business.Constants;

/// <summary>
/// Nomes dos grupos da Mídia Indoor, como cadastrados no Atlas Studio. O nome
/// é a chave: ao cadastrar um parceiro, a operadora põe o responsável em
/// <see cref="ProprietarioParceiro"/> pelo nome.
/// </summary>
public static class BusinessRoles
{
    public const string Operadora = "Operadora";
    public const string ProprietarioParceiro = "Proprietário de Parceiro";
}
