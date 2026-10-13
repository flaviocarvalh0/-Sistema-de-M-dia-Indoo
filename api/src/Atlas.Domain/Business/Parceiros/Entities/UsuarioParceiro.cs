using Atlas.Domain.Business.Parceiros.Enums;

namespace Atlas.Domain.Business.Parceiros.Entities;

/// <summary>
/// Vincula um usuário da plataforma a um parceiro. A Atlas é de empresa única;
/// é este vínculo que permite isolar os dados de cada parceiro no servidor.
/// Um usuário pertence a um único parceiro.
/// </summary>
/// <remarks>
/// Sem <c>[MetadataConfiguration]</c>: não tem tela nem permissão própria — é
/// criado pela operadora junto com a conta do responsável pelo parceiro.
/// </remarks>
public class UsuarioParceiro : BaseAuditableEntity
{
    /// <summary>Id do usuário em <c>platform.user</c>.</summary>
    public long UsuarioId { get; set; }

    public long ParceiroId { get; set; }
    public Parceiro Parceiro { get; set; } = null!;

    public PapelParceiro Papel { get; set; } = PapelParceiro.Proprietario;
    public bool Ativo { get; set; } = true;
}
