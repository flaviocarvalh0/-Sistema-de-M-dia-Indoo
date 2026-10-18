namespace Atlas.Domain.Business.Constants;

/// <summary>
/// Nomes dos grupos da Mídia Indoor, como cadastrados no Atlas Studio. O nome
/// é a chave: ao cadastrar um cliente, a operadora põe o responsável em
/// <see cref="Cliente"/> pelo nome.
/// </summary>
public static class BusinessRoles
{
    public const string Operadora = "Operadora";
    public const string Cliente = "Cliente";
}
