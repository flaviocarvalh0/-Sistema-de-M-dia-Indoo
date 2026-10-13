using Atlas.Application.Common.Extensions;
using Atlas.Application.Platform.SystemMenus.Dtos;
using Atlas.Domain.Platform.Entities;

namespace Atlas.Application.Platform.SystemMenus.Queries.GetSystemMenuTree;

public record GetSystemMenuTreeQuery(long SystemModuleId) : IRequest<Result<List<SystemMenuItemDto>>>;

public class GetSystemMenuTreeQueryHandler : IRequestHandler<GetSystemMenuTreeQuery, Result<List<SystemMenuItemDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;
    private readonly IIdentityService _identityService;

    public GetSystemMenuTreeQueryHandler(
        IApplicationDbContext context,
        IUser user,
        IIdentityService identityService)
    {
        _context = context;
        _user = user;
        _identityService = identityService;
    }

    public async Task<Result<List<SystemMenuItemDto>>> Handle(
        GetSystemMenuTreeQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Verifica se o usuário é superadmin.
        bool isSuperUser = false;
        if (_user.Id.HasValue)
        {
            isSuperUser = await _identityService.IsSuperUserAsync(_user.Id.Value);
        }

        // 2. Busca todos os itens do módulo projetando a shadow property Version.
        var allItemsWithVersion = await _context.SystemMenuItems
            .AsNoTracking()
            .Include(x => x.EntityMetadata)
            .Where(x => x.SystemModuleId == request.SystemModuleId)
            .OrderBy(x => x.SortOrder)
            .Select(x => new MenuItemWithVersion(x, EF.Property<uint>(x, "Version")))
            .ToListAsync(cancellationToken);

        // 3. Carrega as permissões do usuário apenas se ele NÃO for superadmin.
        var userPermissions = (isSuperUser || _user.Id is null || _user.Id == 0)
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(
                await _identityService.GetUserPermissionsAsync(_user.Id.Value, cancellationToken),
                StringComparer.OrdinalIgnoreCase);

        // 4. Filtra os itens com base nas permissões.
        var allowedItems = new List<MenuItemWithVersion>();

        foreach (var itemWithVersion in allItemsWithVersion)
        {
            var item = itemWithVersion.Item;

            if (isSuperUser)
            {
                allowedItems.Add(itemWithVersion);
                continue;
            }

            if (item.MenuType == SystemMenuType.Entity && item.EntityMetadata is not null)
            {
                var prefixoPermissao = item.EntityMetadata.TableName.ToPascalCasePlural();
                var requiredPermission = $"{prefixoPermissao}.View";

                if (userPermissions.Contains(requiredPermission))
                {
                    allowedItems.Add(itemWithVersion);
                }
                continue;
            }

            allowedItems.Add(itemWithVersion);
        }

        // 5. Filtra apenas os nós raiz.
        var rootNodes = allowedItems
            .Where(x => x.Item.ParentId is null)
            .OrderBy(x => x.Item.SortOrder)
            .ToList();

        // 6. Monta a árvore e remove agrupadores que ficarem sem filhos visíveis.
        var tree = rootNodes
            .Select(node => MapToDto(node, allowedItems))
            .Where(dto => dto is not null)
            .Cast<SystemMenuItemDto>()
            .ToList();

        return Result<List<SystemMenuItemDto>>.Success(tree);
    }

    private static SystemMenuItemDto? MapToDto(
        MenuItemWithVersion nodeWithVersion,
        List<MenuItemWithVersion> allowedItems)
    {
        var node = nodeWithVersion.Item;

        var children = allowedItems
            .Where(x => x.Item.ParentId == node.Id)
            .OrderBy(x => x.Item.SortOrder)
            .Select(child => MapToDto(child, allowedItems))
            .Where(childDto => childDto is not null)
            .Cast<SystemMenuItemDto>()
            .ToList();

        // Remove agrupadores que ficaram sem filhos após o filtro de segurança.
        if (node.MenuType == SystemMenuType.Menu && children.Count == 0)
        {
            return null;
        }

        return new SystemMenuItemDto(
            node.Id,
            node.DisplayLabel,
            node.SortOrder,
            node.MenuType.ToString(),
            node.ParentId,
            node.SystemIconId,
            node.IconHexColor,
            node.EntityMetadataId,
            node.EntityMetadata?.RouteKey,
            nodeWithVersion.Version,
            children);
    }

    private record MenuItemWithVersion(SystemMenuItem Item, uint Version);
}
