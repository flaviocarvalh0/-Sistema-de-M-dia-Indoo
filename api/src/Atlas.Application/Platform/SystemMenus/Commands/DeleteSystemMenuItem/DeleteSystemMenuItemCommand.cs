using Atlas.Domain.Platform.Entities;

namespace Atlas.Application.Platform.SystemMenus.Commands.DeleteSystemMenuItem;

public record DeleteSystemMenuItemCommand(
    long Id,
    uint Version) : IRequest<Result>;

public class DeleteSystemMenuItemCommandHandler(IApplicationDbContext context)
    : IRequestHandler<DeleteSystemMenuItemCommand, Result>
{
    public async Task<Result> Handle(
        DeleteSystemMenuItemCommand request,
        CancellationToken cancellationToken)
    {
        var menuItem = await context.SystemMenuItems
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (menuItem is null)
        {
            return Result.Failure(["Item de menu não encontrado."]);
        }

        var allItems = await context.SystemMenuItems
            .Where(x => x.SystemModuleId == menuItem.SystemModuleId)
            .ToListAsync(cancellationToken);

        var idsToDelete = GetDescendantIds(menuItem.Id, allItems);
        idsToDelete.Add(menuItem.Id);

        var itemsToDelete = allItems
            .Where(x => idsToDelete.Contains(x.Id))
            .ToList();

        context.SystemMenuItems.RemoveRange(itemsToDelete);

        context.Entry(menuItem).Property<uint>("Version").OriginalValue = request.Version;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private static HashSet<long> GetDescendantIds(long parentId, List<SystemMenuItem> allItems)
    {
        var result = new HashSet<long>();
        var children = allItems.Where(x => x.ParentId == parentId).ToList();

        foreach (var child in children)
        {
            result.Add(child.Id);
            foreach (var descendantId in GetDescendantIds(child.Id, allItems))
            {
                result.Add(descendantId);
            }
        }

        return result;
    }
}
