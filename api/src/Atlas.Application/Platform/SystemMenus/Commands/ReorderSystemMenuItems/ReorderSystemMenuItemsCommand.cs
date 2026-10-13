namespace Atlas.Application.Platform.SystemMenus.Commands.ReorderSystemMenuItems;

public record ReorderSystemMenuItemsCommand(
    long SystemModuleId,
    List<ReorderSystemMenuItemDto> Items) : IRequest<Result>;

public record ReorderSystemMenuItemDto(
    long Id,
    long? ParentId,
    int SortOrder,
    uint Version);

public class ReorderSystemMenuItemsCommandHandler(IApplicationDbContext context)
    : IRequestHandler<ReorderSystemMenuItemsCommand, Result>
{
    public async Task<Result> Handle(
        ReorderSystemMenuItemsCommand request,
        CancellationToken cancellationToken)
    {
        var itemIds = request.Items
            .Select(x => x.Id)
            .Distinct()
            .ToList();

        var menuItems = await context.SystemMenuItems
            .Where(x => x.SystemModuleId == request.SystemModuleId && itemIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        if (menuItems.Count != itemIds.Count)
        {
            return Result.Failure(["Um ou mais itens de menu não foram encontrados no módulo informado."]);
        }

        foreach (var item in menuItems)
        {
            var requestedItem = request.Items.First(x => x.Id == item.Id);

            item.ParentId = requestedItem.ParentId;
            item.SortOrder = requestedItem.SortOrder;

            context.Entry(item).Property<uint>("Version").OriginalValue = requestedItem.Version;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
