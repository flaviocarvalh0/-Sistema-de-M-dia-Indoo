namespace Atlas.Application.Common.Interfaces;

/// <summary>
/// Responsável por garantir que as entidades marcadas com [MetadataConfiguration] 
/// estejam devidamente registradas na tabela de metadados do sistema.
/// </summary>
public interface IEntityMetadataInitializer
{
    Task EnsureMetadataAsync(CancellationToken cancellationToken = default);
}
