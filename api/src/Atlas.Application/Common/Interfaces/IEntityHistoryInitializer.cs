namespace Atlas.Application.Common.Interfaces;

/// <summary>
/// Inicializa e reconcilia a infraestrutura histórica das entidades
/// habilitadas na plataforma.
/// </summary>
public interface IEntityHistoryInitializer
{
    Task EnsureHistoryInfrastructureAsync(
        CancellationToken cancellationToken = default);
}
