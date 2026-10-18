using Atlas.Domain.Business.Clientes.Enums;

namespace Atlas.Domain.Business.Clientes.Entities;

/// <summary>
/// Vincula um usuário da plataforma a um cliente. A Atlas é de empresa única;
/// é este vínculo que permite isolar os dados de cada cliente no servidor.
/// Um usuário pertence a um único cliente.
/// </summary>
/// <remarks>
/// Sem <c>[MetadataConfiguration]</c>: não tem tela nem permissão própria — é
/// criado pela operadora junto com a conta do responsável pelo cliente.
/// </remarks>
public class UsuarioCliente : BaseAuditableEntity
{
    /// <summary>Id do usuário em <c>platform.user</c>.</summary>
    public long UsuarioId { get; set; }

    public long ClienteId { get; set; }
    public Cliente Cliente { get; set; } = null!;

    public PapelCliente Papel { get; set; } = PapelCliente.Proprietario;
    public bool Ativo { get; set; } = true;
}
