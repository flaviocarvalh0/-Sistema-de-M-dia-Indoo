using Atlas.Domain.Platform.Entities;

namespace Atlas.Application.Platform.SystemModules.Commands.AssociateEntities;

public record AssociateEntitiesCommand(long SystemModuleId, List<long> EntityMetadataIds) : IRequest<Result>;

public class AssociateEntitiesCommandHandler(IApplicationDbContext context)
    : IRequestHandler<AssociateEntitiesCommand, Result>
{
    public async Task<Result> Handle(
        AssociateEntitiesCommand request,
        CancellationToken cancellationToken)
    {
        var moduleExists = await context.SystemModules
            .AnyAsync(x => x.Id == request.SystemModuleId, cancellationToken);

        if (!moduleExists)
        {
            return Result.Failure(["Módulo do sistema não encontrado."]);
        }

        var entityIds = request.EntityMetadataIds
            .Distinct()
            .ToList();

        var entitiesToUpdate = await context.EntityMetadata
            .Where(x => entityIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        if (entitiesToUpdate.Count != entityIds.Count)
        {
            return Result.Failure(["Uma ou mais entidades informadas não foram encontradas."]);
        }

        // Verifica se alguma entidade já está em OUTRO módulo diferente do atual
        var boundToOtherModule = entitiesToUpdate
            .Where(x => x.SystemModuleId.HasValue && x.SystemModuleId != request.SystemModuleId)
            .ToList();

        if (boundToOtherModule.Any())
        {
            var names = string.Join(", ", boundToOtherModule.Select(x => x.TableName));
            return Result.Failure([$"As seguintes entidades já estão associadas a outro módulo: {names}."]);
        }

        using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // Descobre qual é a última ordem de exibição do menu para esse módulo
            var maxSortOrder = await context.SystemMenuItems
                .Where(m => m.SystemModuleId == request.SystemModuleId)
                .Select(m => (int?)m.SortOrder)
                .MaxAsync(cancellationToken) ?? 0;

            foreach (var entity in entitiesToUpdate)
            {
                // 1. Associa a entidade ao Módulo
                entity.SystemModuleId = request.SystemModuleId;

                // 2. Garante que existe o item de menu para esta entidade no módulo
                var menuExists = await context.SystemMenuItems.AnyAsync(
                    m => m.SystemModuleId == request.SystemModuleId && m.EntityMetadataId == entity.Id,
                    cancellationToken);

                if (!menuExists)
                {
                    var menuItem = new SystemMenuItem
                    {
                        DisplayLabel = entity.DisplayLabel,
                        SortOrder = ++maxSortOrder,
                        MenuType = SystemMenuType.Entity,
                        SystemModuleId = request.SystemModuleId,
                        EntityMetadataId = entity.Id,
                        ParentId = null
                    };

                    context.SystemMenuItems.Add(menuItem);
                }
            }

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result.Success();
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
