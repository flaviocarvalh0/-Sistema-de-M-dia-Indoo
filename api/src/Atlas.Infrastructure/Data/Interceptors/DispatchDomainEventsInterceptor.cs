using Atlas.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Atlas.Infrastructure.Data.Interceptors;

/// <summary>
/// Interceptor do Entity Framework responsável por publicar os eventos de domínio
/// acumulados nas entidades durante a operação de persistência.
/// </summary>
/// <remarks>
/// Antes da conclusão do SaveChanges/SaveChangesAsync, localiza todas as entidades rastreadas
/// que possuem eventos de domínio, limpa esses eventos das entidades e os publica via MediatR.
/// </remarks>
public class DispatchDomainEventsInterceptor : SaveChangesInterceptor
{
    private readonly IMediator _mediator;

    public DispatchDomainEventsInterceptor(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, 
        InterceptionResult<int> result)
    {
        DispatchDomainEvents(eventData.Context)
            .GetAwaiter()
            .GetResult();

        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, 
        InterceptionResult<int> result, 
        CancellationToken cancellationToken = default)
    {
        await DispatchDomainEvents(eventData.Context);

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// Coleta e publica todos os eventos de domínio pendentes nas entidades rastreadas
    /// pelo contexto informado.
    /// </summary>
    /// <param name="context">
    /// Contexto do Entity Framework contendo as entidades rastreadas na operação atual.
    /// </param>
    public async Task DispatchDomainEvents(DbContext? context)
    {
        if (context == null) return;

        var entities = context.ChangeTracker
            .Entries<BaseEntity>()
            .Where(e => e.Entity.DomainEvents.Any())
            .Select(e => e.Entity)
            .ToList();

        var domainEvents = entities
            .SelectMany(e => e.DomainEvents)
            .ToList();

        entities.ForEach(e => e.ClearDomainEvents());

        foreach (var domainEvent in domainEvents)
        {
            await _mediator.Publish(domainEvent);
        }
    }
}
