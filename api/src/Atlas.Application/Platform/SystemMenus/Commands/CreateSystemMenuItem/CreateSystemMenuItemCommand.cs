using Atlas.Domain.Platform.Entities;

namespace Atlas.Application.Platform.SystemMenus.Commands.CreateSystemMenuItem;

public record CreatedSystemMenuItemDto(long Id, uint Version);

public record CreateSystemMenuItemCommand(
    string? DisplayLabel,
    int SortOrder,
    SystemMenuType MenuType,
    string? IconHexColor,
    long SystemModuleId,
    long? ParentId,
    long? SystemIconId,
    long? EntityMetadataId) : IRequest<Result<CreatedSystemMenuItemDto>>;

public class CreateSystemMenuItemCommandHandler(IApplicationDbContext context)
    : IRequestHandler<CreateSystemMenuItemCommand, Result<CreatedSystemMenuItemDto>>
{
    public async Task<Result<CreatedSystemMenuItemDto>> Handle(
        CreateSystemMenuItemCommand request,
        CancellationToken cancellationToken)
    {
        var finalLabel = request.DisplayLabel?.Trim();

        if (request.MenuType == SystemMenuType.Entity && string.IsNullOrWhiteSpace(finalLabel))
        {
            var metadata = await context.EntityMetadata
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == request.EntityMetadataId, cancellationToken);

            if (metadata is null)
            {
                return Result<CreatedSystemMenuItemDto>.Failure(["A entidade de metadados associada não foi encontrada."]);
            }

            finalLabel = metadata.DisplayLabel;
        }

        if (string.IsNullOrWhiteSpace(finalLabel))
        {
            return Result<CreatedSystemMenuItemDto>.Failure(["O rótulo de exibição deve ser informado."]);
        }

        var menuItem = new SystemMenuItem
        {
            DisplayLabel = finalLabel,
            SortOrder = request.SortOrder,
            MenuType = request.MenuType,
            IconHexColor = request.IconHexColor,
            SystemModuleId = request.SystemModuleId,
            ParentId = request.ParentId,
            SystemIconId = request.SystemIconId,
            EntityMetadataId = request.MenuType == SystemMenuType.Entity
                ? request.EntityMetadataId
                : null
        };

        context.SystemMenuItems.Add(menuItem);

        await context.SaveChangesAsync(cancellationToken);

        // Captura a versão gerada no PostgreSQL logo após o INSERT
        var createdVersion = context.Entry(menuItem).Property<uint>("Version").CurrentValue;

        return Result<CreatedSystemMenuItemDto>.Success(new CreatedSystemMenuItemDto(menuItem.Id, createdVersion));
    }
}
