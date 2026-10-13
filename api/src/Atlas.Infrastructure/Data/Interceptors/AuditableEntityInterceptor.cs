using Atlas.Application.Common.Interfaces;
using Atlas.Domain.Common;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Atlas.Infrastructure.Data.Interceptors;

/// <summary>
/// Interceptor do Entity Framework responsável por preencher automaticamente
/// os campos de auditoria das entidades que herdam de <see cref="BaseAuditableEntity"/>.
/// </summary>
/// <remarks>
/// Durante o SaveChanges/SaveChangesAsync, define as informações de criação e última alteração
/// com base no usuário autenticado e no horário UTC atual.
/// </remarks>
public class AuditableEntityInterceptor : SaveChangesInterceptor
{
    private readonly IUser _user;
    private readonly TimeProvider _datetime;

    public AuditableEntityInterceptor(
        IUser user,
        TimeProvider datetime)
    {
        _user = user;
        _datetime = datetime;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        UpdateEntities(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        UpdateEntities(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// Atualiza os campos de auditoria das entidades adicionadas ou modificadas
    /// no ChangeTracker do contexto informado.
    /// </summary>
    /// <param name="context">
    /// Contexto do Entity Framework que contém as entidades rastreadas na operação atual.
    /// </param>
    public void UpdateEntities(DbContext? context)
    {
        if (context == null) return;

        foreach (var entry in context.ChangeTracker.Entries<BaseAuditableEntity>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified || entry.HasChangedOwnedEntities())
            {
                var utcNow = _datetime.GetUtcNow();
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedBy ??= _user.Id;
                    entry.Entity.Created = utcNow;
                }
                entry.Entity.LastModifiedBy ??= _user.Id;
                entry.Entity.LastModified = utcNow;
            }
        }
    }
}

/// <summary>
/// Extensões auxiliares para avaliação de mudanças em entidades rastreadas pelo Entity Framework.
/// </summary>
public static class Extensions
{
    /// <summary>
    /// Verifica se a entidade possui objetos de valor/entidades owned alterados,
    /// garantindo que mudanças em tipos owned também atualizem os campos de auditoria da entidade principal.
    /// </summary>
    public static bool HasChangedOwnedEntities(this EntityEntry entry) =>
        entry.References.Any(r =>
            r.TargetEntry != null &&
            r.TargetEntry.Metadata.IsOwned() &&
            (r.TargetEntry.State == EntityState.Added || r.TargetEntry.State == EntityState.Modified));
}
