using Atlas.Application.Platform.SystemMenus.Dtos;
using Atlas.Domain.Platform.Entities;

namespace Atlas.Application.Platform.SystemMenus.Queries.GetModuleMenuTree;

public record GetModuleMenuTreeQuery(long ModuleId) : IRequest<Result<List<SystemMenuItemDto>>>;

public class GetModuleMenuTreeQueryHandler : IRequestHandler<GetModuleMenuTreeQuery, Result<List<SystemMenuItemDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetModuleMenuTreeQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<SystemMenuItemDto>>> Handle(GetModuleMenuTreeQuery request, CancellationToken cancellationToken)
    {
        var itemsWithVersion = await _context.SystemMenuItems
            .AsNoTracking()
            .Include(x => x.EntityMetadata)
            .Where(x => x.SystemModuleId == request.ModuleId)
            .OrderBy(x => x.SortOrder)
            .Select(x => new MenuWithVersion(x, EF.Property<uint>(x, "Version")))
            .ToListAsync(cancellationToken);

        var tree = itemsWithVersion
            .Where(x => x.Item.ParentId == null)
            .Select(node => MapToDto(node, itemsWithVersion))
            .ToList();

        return Result<List<SystemMenuItemDto>>.Success(tree);
    }

    private static SystemMenuItemDto MapToDto(
        MenuWithVersion node,
        List<MenuWithVersion> allItems)
    {
        var children = allItems
            .Where(x => x.Item.ParentId == node.Item.Id)
            .OrderBy(x => x.Item.SortOrder)
            .Select(child => MapToDto(child, allItems))
            .ToList();

        return new SystemMenuItemDto(
            node.Item.Id,
            node.Item.DisplayLabel,
            node.Item.SortOrder,
            node.Item.MenuType.ToString(),
            node.Item.ParentId,
            node.Item.SystemIconId,
            node.Item.IconHexColor,
            node.Item.EntityMetadataId,
            node.Item.EntityMetadata?.RouteKey,
            node.Version,
            children);
    }

    private record MenuWithVersion(SystemMenuItem Item, uint Version);
}
